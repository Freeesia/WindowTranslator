using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public sealed class OcrCorrectionFilter : IFilterModule, IDisposable
{
    private readonly ConcurrentDictionary<string, string?> cache = new();
    private readonly Channel<IReadOnlyList<string>> queue;
    private readonly GenerativeModel? client;
    private readonly ILogger<OcrCorrectionFilter> logger;
    private readonly CancellationTokenSource cts = new();

    public OcrCorrectionFilter(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GoogleAIOptions> googleAiOptions, ILogger<OcrCorrectionFilter> logger)
    {
        var options = googleAiOptions.Value;
        var system = $"""
        あなたは{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}の専門家です。
        これから渡される文字列はOCRによって認識されたものであり、誤字や脱字が含まれています。
        次の指示に従って、渡された文字列を修正してください。

        1. 誤字や脱字を、文字形の類似性を考慮して修正してください。
        2. 単語や文章の意味が崩れないように、文字数の変更は最小限にしてください。
        3. 正しい単語や文章に修正した結果のみを出力してください。
        4. 誤字や脱字がない場合は、そのままの文字列を出力してください。
        5. 単語や文章として認識できない場合は、空文字を出力してください。

        <誤字修正の例>
        {options.CorrectSample}
        </誤字修正の例>

        修正する文字列は以下のJsonフォーマットになっています。出力文字列も同じJsonフォーマットで、入力文字列の順序を維持してください。
        <入力テキストのJsonフォーマット>
        ["誤字修正するテキスト1","誤字修正するテキスト2"]
        </入力テキストのJsonフォーマット>
        """;
        this.logger = logger;
        this.queue = Channel.CreateBounded<IReadOnlyList<string>>(new(1)
        {
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        }, Dropped);
        if (options.IsEnabledCorrect && options.ApiKey is { Length: > 0 } apiKey)
        {
            var googleAI = new GoogleAi(apiKey, logger: logger);
            this.client = googleAI.CreateGenerativeModel(
                string.IsNullOrEmpty(options.PreviewModel) ? options.Model.GetName() : options.PreviewModel,
                safetyRatings: [
                    new(){ Category = HarmCategory.HARM_CATEGORY_HARASSMENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                    new(){ Category = HarmCategory.HARM_CATEGORY_HATE_SPEECH, Threshold =HarmBlockThreshold.BLOCK_NONE},
                    new(){ Category = HarmCategory.HARM_CATEGORY_SEXUALLY_EXPLICIT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                    new(){ Category = HarmCategory.HARM_CATEGORY_DANGEROUS_CONTENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                ],
                systemInstruction: system);
            Task.Run(Correct, this.cts.Token);
        }
    }

    public void Dispose()
    {
        this.queue.Writer.Complete();
        this.cts.Cancel();
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
        => texts;

    public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
    {
        if (this.client is null)
        {
            await foreach (var text in texts.ConfigureAwait(false))
            {
                yield return text;
            }
            yield break;
        }
        var targets = new List<string>();
        await foreach (var text in texts.ConfigureAwait(false))
        {
            if (!this.cache.TryGetValue(text.Text, out var corrected))
            {
                targets.Add(text.Text);
            }
            if (corrected is not null)
            {
                yield return text.Text != corrected ? text with { Text = corrected } : text;
            }
            else
            {
                yield return text;
            }
        }

        if (targets.Count > 0)
        {
            await this.queue.Writer.WriteAsync(targets, this.cts.Token).ConfigureAwait(false);
        }
    }

    private async Task Correct()
    {
        await foreach (var texts in this.queue.Reader.ReadAllAsync(this.cts.Token))
        {
            foreach (var text in texts)
            {
                this.cache.TryAdd(text, null);
            }
            try
            {
                var corrected = await this.client!.GenerateObjectAsync<string[]>(JsonSerializer.Serialize(texts, DefaultSerializerOptions.GenerateObjectJsonOptions))
                    .ConfigureAwait(false) ?? [];
                this.cts.Token.ThrowIfCancellationRequested();
                for (var i = 0; i < texts.Count; i++)
                {
                    this.cache[texts[i]] = corrected[i];
                }
            }
            catch (Exception e)
            {
                this.logger.LogError(e, $"Failed to correct `{texts}`");
            }
        }
    }

    private void Dropped(IReadOnlyList<string> texts)
        => this.logger.LogDebug($"Dropped texts: {string.Join(", ", texts)}");
}
