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
    private static readonly ChatMessage assitant = ChatMessage.CreateAssistantMessage("[");
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true,
    };
    private static readonly ChatCompletionOptions openAiOptions = new()
    {
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            "corrected_array",
            BinaryData.FromBytes("""
                {
                    "type": "array",
                    "items": { "type": "string" }
                }
                """u8.ToArray()),
            "補正後のテキストの配列",
            true),
        StopSequences = { "\"]" },
    };

    protected override CorrectMode TargetMode => CorrectMode.Text;

    private readonly ChatMessage system = ChatMessage.CreateSystemMessage($"""
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


    protected override ValueTask<IReadOnlyList<string>> GetQueueData(IEnumerable<TextRect> targets, FilterContext context)
        => new([.. targets.Select(t => t.Text)]);

    protected override async Task CorrectCore(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        foreach (var text in texts)
        {
            this.Cache.TryAdd(text, null);
        }
        try
        {
            var ums = ChatMessage.CreateUserMessage(JsonSerializer.Serialize(texts, jsonOptions));
            ChatCompletion completion = await this.Client.CompleteChatAsync([this.system, ums, assitant], openAiOptions, cancellationToken)
                .ConfigureAwait(false);
            var json = completion.Content[0].Text.Trim();
            if (!json.StartsWith('['))
            {
                json = "[" + json;
            }
            if (!json.EndsWith(']'))
            {
                json += "]";
            }
            var corrected = JsonSerializer.Deserialize<string[]>(json) ?? [];
            for (var i = 0; i < texts.Count; i++)
            {
                this.Cache[texts[i]] = corrected[i];
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, $"Failed to correct `{texts}`");
        }
    }

    protected override void Dropped(IReadOnlyList<string> texts)
        => this.Logger.LogDebug($"Dropped texts: {string.Join(", ", texts)}");
}
