using System.Globalization;
using System.Text.Json;
using GenerativeAI.Helpers;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public class GoogleAITranslator : ITranslateModule
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string preSystem;
    private readonly string postSystem;
    private readonly GenerativeModelEx? client;
    private IReadOnlyDictionary<string, string> glossary = new Dictionary<string, string>();
    private string? context;

    public GoogleAITranslator(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GoogleAIOptions> googleAiOptions)
    {
        var src = CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName;
        var target = CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName;
        this.preSystem = $$"""
        あなたは{{src}}から{{target}}へ翻訳するの専門家です。
        入力テキストは{{src}}のテキストであり、翻訳が必要です。
        渡されたテキストを{{target}}へ翻訳して出力してください。
        """;
        this.postSystem = """
        入力テキストは以下のJsonフォーマットになっています。        
        <入力テキストのJsonフォーマット>
        [{"text":"翻訳対象のテキスト1", "context": "翻訳対象のテキスト1の文脈"}, {"text":"翻訳対象のテキスト2", "context": "翻訳対象のテキスト2の文脈"}]
        </入力テキストのJsonフォーマット>
        
        出力は以下の文字列型の配列のJsonフォーマットです。
        入力されたテキストの順序を維持して翻訳したテキストを出力してください。
        <出力テキストのJsonフォーマット>
        ["翻訳したテキスト1", "翻訳したテキスト2"]
        </出力テキストのJsonフォーマット>
        """;
        if (!(googleAiOptions.Value.ApiKey is { Length: > 0 } apiKey))
        {
            return;
        }
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
                SafetySettings = [
                    new(){ Category = HarmCategory.HARM_CATEGORY_HARASSMENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                    new(){ Category = HarmCategory.HARM_CATEGORY_HATE_SPEECH, Threshold =HarmBlockThreshold.BLOCK_NONE},
                    new(){ Category = HarmCategory.HARM_CATEGORY_SEXUALLY_EXPLICIT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                    new(){ Category = HarmCategory.HARM_CATEGORY_DANGEROUS_CONTENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                ]
            });
    }

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        if (this.client is null)
        {
            throw new InvalidOperationException("GoogleAI機能が初期化されていません。設定ダイアログからGoogleAIオプションを設定してください");
        }
        var glossary = this.glossary.Where(kv => srcTexts.Any(s => s.Text.Contains(kv.Key))).ToArray();
        var glossarySystem = glossary.Length > 0 ? $"""
            翻訳する際に以下の用語集を参照して、一貫した翻訳を行ってください。
            <用語集>
            {string.Join(Environment.NewLine, glossary.Select(kv => $"<用語>{kv.Key}</用語><翻訳>{kv.Value}</翻訳>"))}
            </用語集>
            """ : string.Empty;
        var req = new GenerateContentRequest()
        {
            Contents = [RequestExtensions.FormatGenerateContentInput(JsonSerializer.Serialize(srcTexts.Select(s => new { s.Text, s.Context }).ToArray(), jsonOptions))],
            SystemInstruction = RequestExtensions.FormatSystemInstruction(string.Join(Environment.NewLine, [this.preSystem, this.context, glossarySystem, this.postSystem])),
        };
        var completion = await this.client.GenerateContentAsync(req).ConfigureAwait(false);
        return completion is null ? [] : JsonSerializer.Deserialize<string[]>(completion.Text() + "\"]", jsonOptions) ?? [];
    }

    public ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        this.glossary = glossary;
        return default;
    }

    public void RegisterContext(string context)
        => this.context = $"""
        翻訳するテキストは全体を通して、以下の背景や文脈があるものして翻訳してください。
        <背景>
        {context}
        </背景>
        """;
}
