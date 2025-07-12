using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.LLMPlugin;

public class OcrCorrectionTextFilter(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<LLMOptions> llmOptions, ILogger<OcrCorrectionTextFilter> logger)
    : OcrCorrectFilterBase<string>(llmOptions, logger)
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };
    private static readonly ChatCompletionOptions openAiOptions = new()
    {
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            "corrected_texts",
            BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "texts": {
                        "type": "array",
                        "items": { "type": "string" }
                    }
                },
                "additionalProperties": false,
                "required": ["texts"]
            }
            """u8.ToArray()),
            "補正後のテキストの配列",
            true),
    };

    protected override CorrectMode TargetMode => CorrectMode.Text;

    private readonly ChatMessage system = ChatMessage.CreateSystemMessage($$"""
        あなたは{{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}}の専門家です。
        これから渡される文字列はOCRによって認識されたものであり、誤字や脱字が含まれています。
        次の指示に従って、渡された文字列を修正してください。

        1. 誤字や脱字を、文字形の類似性を考慮して修正してください。
        2. 単語や文章の意味が崩れないように、文字数の変更は最小限にしてください。
        3. 正しい単語や文章に修正した結果のみを出力してください。
        4. 誤字や脱字がない場合は、そのままの文字列を出力してください。
        5. 単語や文章として認識できない場合は、空文字を出力してください。

        ## 誤字修正の例
        {{llmOptions.Value.CorrectSample}}

        ## フォーマット

        入力される修正対象の文字列は以下のJsonフォーマットになっています。
        入力テキストのJsonフォーマット:
        ```json
        ["誤字修正するテキスト1","誤字修正するテキスト2"]
        ```

        出力文字列は以下のJsonフォーマットになっています。必ず入力したテキストと同じ順番で出力してください。
        出力テキストのJsonフォーマット:
        ```json
        {
            "texts": ["修正後のテキスト1","修正後のテキスト2"]
        }
        ```
        ただし、出力テキストは必ず1行で出力してください。
        """);


    protected override ValueTask<IReadOnlyList<string>> GetQueueData(IEnumerable<TextRect> targets, FilterContext context)
        => new([.. targets.Select(t => t.SourceText)]);

    protected override async Task CorrectCore(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        foreach (var text in texts)
        {
            this.Cache.TryAdd(text, null);
        }
        try
        {
            var ums = ChatMessage.CreateUserMessage(JsonSerializer.Serialize(texts, jsonOptions));
            ChatCompletion completion = await this.Client.CompleteChatAsync([this.system, ums], openAiOptions, cancellationToken)
                .ConfigureAwait(false);
            var json = completion.Content[0].Text.Trim();
            var corrected = JsonSerializer.Deserialize<RecognizedTexts>(json, jsonOptions)?.Texts ?? [];
            for (var i = 0; i < texts.Count; i++)
            {
                this.Cache[texts[i]] = corrected[i];
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, $"Failed to correct `{texts}`");
        }
        this.Logger.LogDebug($"Correct: {sw.Elapsed}");
    }

    protected override void Dropped(IReadOnlyList<string> texts)
        => this.Logger.LogDebug($"Dropped texts: {string.Join(", ", texts)}");

    private record RecognizedTexts(string[] Texts);
}
