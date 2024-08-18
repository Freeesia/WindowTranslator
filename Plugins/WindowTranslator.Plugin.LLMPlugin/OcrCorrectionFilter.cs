using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.LLMPlugin;

public class OcrCorrectionFilter : IFilterModule
{
    private static readonly ChatMessage assitant = ChatMessage.CreateAssistantMessage("[\"");
    private readonly ConcurrentDictionary<string, string?> cache = new();
    private readonly Channel<IReadOnlyList<string>> queue;
    private readonly ChatClient? client;
    private readonly ChatMessage system;
    private readonly ILogger<OcrCorrectionFilter> logger;

    public OcrCorrectionFilter(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<LLMOptions> llmOptions, ILogger<OcrCorrectionFilter> logger)
    {
        this.system = ChatMessage.CreateSystemMessage($"""
        あなたは{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}の専門家です。
        これから渡される文字列はOCRによって認識されたものであり、誤字や脱字が含まれています。
        次の指示に従って、渡された文字列を修正してください。

        1. 誤字や脱字を、文字形の類似性を考慮して修正してください。
        2. 単語や文章の意味が崩れないように、文字数の変更は最小限にしてください。
        3. 正しい単語や文章に修正した結果のみを出力してください。
        4. 誤字や脱字がない場合は、そのままの文字列を出力してください。
        5. 単語や文章として認識できない場合は、空文字を出力してください。

        <誤字修正の例>
        {llmOptions.Value.CorrectSample}
        </誤字修正の例>

        修正する文字列は以下のJsonフォーマットになっています。出力文字列も同じJsonフォーマットで、入力文字列の順序を維持してください。
        <入力テキストのJsonフォーマット>
        ["誤字修正するテキスト1","誤字修正するテキスト2"]
        </入力テキストのJsonフォーマット>
        """);
        this.logger = logger;
        this.queue = Channel.CreateBounded<IReadOnlyList<string>>(new(1)
        {
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        }, Dropped);
        if (llmOptions.Value.IsEnabledCorrect && llmOptions.Value.Model is { Length: > 0 } model && llmOptions.Value.ApiKey is { Length: > 0 } apiKey)
        {
            this.client = new(
                model,
                apiKey,
                llmOptions.Value.Endpoint is { Length: > 0 } e ? new OpenAIClientOptions() { Endpoint = new(e) } : null);
            Task.Run(Correct);
        }
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts)
        => texts;

    public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts)
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
            await this.queue.Writer.WriteAsync(targets).ConfigureAwait(false);
        }
    }

    private async Task Correct()
    {
        await foreach (var texts in this.queue.Reader.ReadAllAsync())
        {
            foreach (var text in texts)
            {
                this.cache.TryAdd(text, null);
            }
            try
            {
                var ums = ChatMessage.CreateUserMessage(JsonSerializer.Serialize(texts));
                var completion = await this.client!.CompleteChatAsync([this.system, ums, assitant],
                    new() { StopSequences = { "\"]" } })
                    .ConfigureAwait(false);
                var json = assitant.Content[0].Text + completion.Value.ToString().Trim() + "\"]";
                var corrected = JsonSerializer.Deserialize<string[]>(json) ?? [];
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
