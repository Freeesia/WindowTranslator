using System.Globalization;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GitHubCopilotPlugin.Properties;

namespace WindowTranslator.Plugin.GitHubCopilotPlugin;

public class GitHubCopilotTranslator : ITranslateModule, IAsyncDisposable
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
    };

    private readonly string preSystem;
    private readonly string? userContext;
    private readonly string postSystem;
    private readonly string model;
    private readonly IDictionary<string, string> glossary = new Dictionary<string, string>();
    private readonly CopilotClient client;
    private IReadOnlyList<string> common = [];
    private string? context;

    public string Name => $"{nameof(GitHubCopilotTranslator)}: {this.model}";

    public GitHubCopilotTranslator(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GitHubCopilotOptions> options)
    {
        var srcLang = CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName;
        var targetLang = CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName;
        this.model = options.Value.Model;
        this.userContext = options.Value.TranslateContext;

        this.preSystem = $$"""
        あなたは{{srcLang}}から{{targetLang}}へ翻訳するの専門家です。
        入力テキストは{{srcLang}}のテキストであり、翻訳が必要です。
        渡されたテキストを{{targetLang}}へ翻訳して出力してください。
        """;
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

        this.client = new CopilotClient();

        if (File.Exists(options.Value.GlossaryPath))
        {
            using var reader = new StreamReader(options.Value.GlossaryPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false });
            foreach (var (src, dst) in csv.GetRecords<Glossary>())
            {
                this.glossary[src] = dst;
            }
        }
    }

    private record Glossary(string Source, string Target);

    private record Response(string[] Translated);

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
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
        var content = JsonSerializer.Serialize(srcTexts.Select(s => new { text = s.SourceText, context = s.Context }).ToArray(), jsonOptions);

        await using var session = await this.client.CreateSessionAsync(new SessionConfig
        {
            Model = this.model,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = system,
            },
        }).ConfigureAwait(false);

        var response = await session.SendAndWaitAsync(new MessageOptions { Prompt = content }).ConfigureAwait(false);
        var json = response?.Data?.Content?.Trim() ?? string.Empty;
        var res = JsonSerializer.Deserialize<Response>(json, jsonOptions);
        return res?.Translated ?? [];
    }

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
        => this.context = $"""
        翻訳するテキストは全体を通して、以下の背景や文脈があるものして翻訳してください。
        <背景>
        {context}
        </背景>

        """;

    public async ValueTask DisposeAsync()
    {
        await this.client.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
