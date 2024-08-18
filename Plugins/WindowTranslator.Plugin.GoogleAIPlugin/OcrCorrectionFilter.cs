using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using GenerativeAI.Exceptions;
using GenerativeAI.Helpers;
using GenerativeAI.Models;
using GenerativeAI.Requests;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;
using System.Net.Http.Json;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public class OcrCorrectionFilter : IFilterModule
{
    private readonly ConcurrentDictionary<string, string?> cache = new();
    private readonly Channel<IReadOnlyList<string>> queue;
    private readonly GenerativeModelEx? client;
    private readonly ILogger<OcrCorrectionFilter> logger;

    public OcrCorrectionFilter(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GoogleAIOptions> googleAiOptions, ILogger<OcrCorrectionFilter> logger)
    {
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
        {googleAiOptions.Value.CorrectSample}
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
        if (googleAiOptions.Value.IsEnabledCorrect && googleAiOptions.Value.ApiKey is { Length: > 0 } apiKey)
        {
            this.client = new(
                apiKey,
                new()
                {
                    Model = googleAiOptions.Value.Model.GetName(),
                    GenerationConfig = new GenerationConfigEx()
                    {
                        Temperature = 2.0,
                        StopSequences = ["\"]"],
                        ResponseMimeType = "application/json",
                    },
                },
                system);
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
                var completion = await this.client!.GenerateContentAsync(JsonSerializer.Serialize(texts))
                    .ConfigureAwait(false);
                var corrected = JsonSerializer.Deserialize<string[]>(completion + "\"]") ?? [];
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

public class GenerativeModelEx(string apiKey, ModelParams modelParams, string system) : GenerativeModel(apiKey, modelParams, systemInstruction: system)
{
    protected override async Task<EnhancedGenerateContentResponse> GenerateContent(string apiKey, string model, GenerateContentRequest request)
    {
        var url = new RequestUrl(model, Tasks.GenerateContent, apiKey, false, BaseUrl, Version);
        request.SystemInstruction ??= RequestExtensions.FormatSystemInstruction(this.SystemInstruction);
        var serializerOptions = SerializerOptions;
        serializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        serializerOptions.TypeInfoResolver = PolymorphicTypeResolver.Instance;

        var response = await Client.PostAsJsonAsync(url, request, serializerOptions).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<EnhancedGenerateContentResponse>(serializerOptions).ConfigureAwait(false);

            if (result!.Candidates is not { Length: > 0 })
            {
                var blockErrorMessage = ResponseHelper.FormatBlockErrorMessage(result);
                if (!string.IsNullOrEmpty(blockErrorMessage))
                {
                    throw new GenerativeAIException($"Error while requesting {url.ToString("__API_Key__")}:\r\n\r\n{blockErrorMessage}", blockErrorMessage);
                }
            }

            return result;
        }
        else
        {
            var content = await response.Content.ReadAsStringAsync();

            throw new GenerativeAIException($"Error while requesting {url.ToString("__API_Key__")}:\r\n\r\n{content}", content);
        }
    }

    private class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
    {
        public static readonly PolymorphicTypeResolver Instance = new();
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var jsonTypeInfo = base.GetTypeInfo(type, options);
            if (jsonTypeInfo.Type == typeof(GenerationConfig))
            {
                jsonTypeInfo.PolymorphismOptions = new()
                {
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor,
                    DerivedTypes = { new(typeof(GenerationConfigEx)) },
                };
            }

            return jsonTypeInfo;
        }
    }
}

public class GenerationConfigEx : GenerationConfig
{
    public string ResponseMimeType { get; set; } = "text/plain";
}