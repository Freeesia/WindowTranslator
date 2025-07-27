using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using GenerativeAI;
using GenerativeAI.Exceptions;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GoogleAIPlugin.Properties;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public class GoogleAITranslator : ITranslateModule
{
    private readonly string preSystem;
    private readonly string? userContext;
    private readonly string postSystem;
    private readonly GenerativeModel? client;
    private readonly ILogger<GoogleAITranslator> logger;
    private readonly IDictionary<string, string> glossary = new Dictionary<string, string>();
    private IReadOnlyList<string> common = [];
    private string? context;

    public string Name => $"{nameof(GoogleAITranslator)}: {this.client?.Model ?? "Invalid"}";

    public GoogleAITranslator(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GoogleAIOptions> googleAiOptions, ILogger<GoogleAITranslator> logger)
    {
        this.logger = logger;
        var srcLang = CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName;
        var targetLang = CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName;
        var options = googleAiOptions.Value;
        this.preSystem = $$"""
        あなたは{{srcLang}}から{{targetLang}}へ翻訳するの専門家です。
        入力テキストは{{srcLang}}のテキストであり、翻訳が必要です。
        渡されたテキストを{{targetLang}}へ翻訳して出力してください。
        """;
        this.userContext = options.TranslateContext;
        this.postSystem = """
        入力テキストは以下のJsonフォーマットになっています。
        各textの内容はペアとなるcontextの文脈を考慮して翻訳してください。
        contextに一人称が指定されている場合は、漢字、ひらがな、カタカナの表記を変更せずに一人称をそのまま使ってください。
        翻訳対象のテキストが判別できない場合は、翻訳を行わずにそのままの表記を利用してください。
        <入力テキストのJsonフォーマット>
        [{"text":"翻訳対象のテキスト1", "context": "翻訳対象のテキスト1の文脈"}, {"text":"翻訳対象のテキスト2", "context": "翻訳対象のテキスト2の文脈"}]
        </入力テキストのJsonフォーマット>
        
        出力は以下の文字列型の配列のJsonフォーマットです。
        入力されたテキストの順序を維持して翻訳したテキストを出力してください。
        <出力テキストのJsonフォーマット>
        ["翻訳したテキスト1", "翻訳したテキスト2"]
        </出力テキストのJsonフォーマット>
        """;
        if (options.ApiKey is not { Length: > 0 } apiKey)
        {
            return;
        }
        var googleAI = new GoogleAi(apiKey, logger: logger);
        this.client = googleAI.CreateGenerativeModel(
            string.IsNullOrEmpty(options.PreviewModel) ? options.Model.GetName() : options.PreviewModel,
            safetyRatings: [
                new(){ Category = HarmCategory.HARM_CATEGORY_HARASSMENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_HATE_SPEECH, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_SEXUALLY_EXPLICIT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_DANGEROUS_CONTENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
            ]);

        if (File.Exists(googleAiOptions.Value.GlossaryPath))
        {
            using var reader = new StreamReader(googleAiOptions.Value.GlossaryPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false });
            foreach (var (src, dst) in csv.GetRecords<Glossary>())
            {
                this.glossary[src] = dst;
            }
        }
    }

    private record Glossary(string Source, string Target);

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        if (this.client is null)
        {
            throw new InvalidOperationException(Resources.GeminiNotInitialized);
        }
        var glossary = this.glossary.Where(kv => srcTexts.Any(s => s.SourceText.Contains(kv.Key))).ToArray();
        var common = this.common.Where(c => srcTexts.Any(s => s.SourceText.Contains(c))).ToArray();
        var sb = new StringBuilder();
        if (glossary.Length > 0)
        {
            sb.AppendLine($"""
            翻訳する際に以下の用語集を参照して、一貫した翻訳を行ってください。
            <用語集>
            {string.Join(Environment.NewLine, glossary.Select(kv => $"<用語>{kv.Key}</用語><翻訳>{kv.Value}</翻訳>"))}
            </用語集>

            """);
        }
        if (common.Length > 0)
        {
            sb.AppendLine($"""
            翻訳するテキストに以下の共通の用語が含まれている場合は、その用語のみは必ず翻訳せずにそのままの表記を利用してください。
            <共通の用語>
            {string.Join(Environment.NewLine, common)}
            </共通の用語>

            """);
        }

        var system = string.Join(Environment.NewLine, [this.preSystem, this.context, sb, this.userContext, this.postSystem]);
        var content = JsonSerializer.Serialize(srcTexts.Select(s => new { s.SourceText, s.Context }).ToArray(), DefaultSerializerOptions.GenerateObjectJsonOptions);
        this.logger.LogDebug($"""
            System:
            {system}
            Contents:
            {content}
            """);
        var req = new GenerateContentRequest()
        {
            SystemInstruction = RequestExtensions.FormatSystemInstruction(system),
            Contents = [RequestExtensions.FormatGenerateContentInput(content)],
        };
        while (true)
        {
            try
            {
                //return await this.client.GenerateObjectAsync<string[]>(req).ConfigureAwait(false) ?? [];
                var res = await this.client.GenerateContentAsync<string[]>(req).ConfigureAwait(false);
                return res.ToObject<string[]>() ?? [];
            }
            catch (ApiException e) when (e.ErrorCode == 400)
            {
                throw new ApiException(e.ErrorCode, Resources.InvalidApiKey, e.ErrorStatus);
            }
            // サービスが一時的に過負荷になっているか、ダウンしている可能性があります。
            catch (ApiException e) when (e.ErrorCode == 503)
            {
                this.logger.LogWarning("Geminiのサービスが一時的に過負荷になっているか、ダウンしている可能性があります。500ミリ秒待機して再試行します。");
                await Task.Delay(500).ConfigureAwait(false);
                continue;
            }
            // レート制限を超えました。
            catch (ApiException e) when (e.ErrorCode == 429)
            {
                this.logger.LogWarning("Geminiのレート制限を超えました。10秒待機して再試行します。");
                await Task.Delay(10000).ConfigureAwait(false);
                continue;
            }
            // Jsonエラーということは指定した以外のレスポンスが返ってきたのでもう一度
            catch (JsonException)
            {
                continue;
            }
        }
    }

    public ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        this.common = glossary.Where(kv => kv.Key == kv.Value).Select(kv => kv.Key).ToArray();
        foreach (var (key, value) in glossary.Where(kv => kv.Key != kv.Value))
        {
            this.glossary.TryAdd(key, value);
        }
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
