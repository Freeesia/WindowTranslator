using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.PLaMoPlugin.Properties;

namespace WindowTranslator.Plugin.PLaMoPlugin;

[DisplayName("PLaMo")]
public sealed class PLaMoTranslator : ITranslateModule, IDisposable
{
    private readonly string preSystem;
    private readonly string? userContext;
    private readonly string postSystem;
    private readonly LLamaWeights? weights;
    private readonly ModelParams? modelParams;
    private readonly IDictionary<string, string> glossary = new Dictionary<string, string>();
    private IReadOnlyList<string> common = [];
    private string? contextText;

    public PLaMoTranslator(IOptionsSnapshot<PLaMoOptions> plamoOptions, IOptionsSnapshot<LanguageOptions> langOptions)
    {
        var srcLang = CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName;
        var targetLang = CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName;
        var options = plamoOptions.Value;

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
        
        出力は以下の文字列型の配列を持ったJsonフォーマットです。
        入力されたテキストの順序を維持して翻訳したテキストを出力してください。
        <出力テキストのJsonフォーマット>
        {"translated": ["翻訳したテキスト1", "翻訳したテキスト2"]}
        </出力テキストのJsonフォーマット>
        """;

        if (string.IsNullOrEmpty(options.ModelPath))
        {
            throw new AppUserException(Resources.ModelPathNotSet);
        }

        if (!File.Exists(options.ModelPath))
        {
            throw new AppUserException(Resources.ModelFileNotFound);
        }

        this.modelParams = new ModelParams(options.ModelPath)
        {
            ContextSize = (uint)options.ContextSize,
            GpuLayerCount = options.GpuLayerCount,
        };

        this.weights = LLamaWeights.LoadFromFile(this.modelParams);

        if (File.Exists(options.GlossaryPath))
        {
            using var reader = new StreamReader(options.GlossaryPath);
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
        if (this.weights is null || this.modelParams is null)
        {
            throw new InvalidOperationException(Resources.ModelNotInitialized);
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

        var system = string.Join(Environment.NewLine, [this.preSystem, this.contextText, sb, this.userContext, this.postSystem]);
        var userMessage = JsonSerializer.Serialize(srcTexts.Select(s => new { s.SourceText, s.Context }).ToArray());

        var prompt = $"""
        {system}

        {userMessage}
        """;

        using var context = this.weights.CreateContext(this.modelParams);
        var executor = new StatelessExecutor(this.weights, this.modelParams);
        
        var inferenceParams = new InferenceParams
        {
            MaxTokens = 2048,
            AntiPrompts = ["}"],
        };

        var responseBuilder = new StringBuilder();
        
        await foreach (var token in executor.InferAsync(prompt, inferenceParams))
        {
            responseBuilder.Append(token);
            if (token.Contains("}"))
            {
                break;
            }
        }

        var response = responseBuilder.ToString();
        
        // JSONの補完
        if (!response.TrimStart().StartsWith('{'))
        {
            response = "{\"translated\": [" + response;
        }
        if (!response.TrimEnd().EndsWith('}'))
        {
            response += "]}";
        }

        try
        {
            var result = JsonSerializer.Deserialize<Response>(response);
            return result?.Translated ?? [];
        }
        catch (JsonException)
        {
            // JSONパースに失敗した場合は空配列を返す
            return [];
        }
    }

    private record Response(string[] Translated);

    public ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        this.common = glossary.Where(kv => kv.Key == kv.Value).Select(kv => kv.Key.ReplaceLineEndings(string.Empty)).ToArray();
        foreach (var (key, value) in glossary.Where(kv => kv.Key != kv.Value))
        {
            this.glossary.TryAdd(key.ReplaceLineEndings(string.Empty), value.ReplaceLineEndings(string.Empty));
        }

        return default;
    }

    public void RegisterContext(string context)
        => this.contextText = $"""
        翻訳するテキストは全体を通して、以下の背景や文脈があるものして翻訳してください。
        <背景>
        {context}
        </背景>

        """;

    public void Dispose()
    {
        this.weights?.Dispose();
    }
}
