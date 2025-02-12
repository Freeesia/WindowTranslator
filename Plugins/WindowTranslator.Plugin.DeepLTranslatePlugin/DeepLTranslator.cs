using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Serialization;
using CsvHelper;
using CsvHelper.Configuration;
using DeepL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropertyTools.DataAnnotations;
using ValueTaskSupplement;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Stores;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.DeepLTranslatePlugin;

[DefaultModule]
[DisplayName("DeepL")]
public class DeepLTranslator : ITranslateModule
{
    private readonly Translator translator;
    private readonly string sourceLang;
    private readonly string targetLang;
    private readonly IProcessInfoStore processInfo;
    private readonly ILogger<DeepLTranslator> logger;
    private readonly IDictionary<string, string> glossary = new Dictionary<string, string>();
    private AsyncLazy<string?> glossaryId = new(static () => ValueTask.FromResult<string?>(null));
    private string? context = null;

    public DeepLTranslator(
        IProcessInfoStore processInfo,
        IOptionsSnapshot<DeepLOptions> deeplOptions,
        IOptionsSnapshot<LanguageOptions> langOptions,
        ILogger<DeepLTranslator> logger)
    {
        this.translator = new(deeplOptions.Value.AuthKey);
        this.sourceLang = langOptions.Value.Source[..2];
        this.targetLang = langOptions.Value.Target switch
        {
            "en-US" or "en-GB" or "pt-BR" or "pt-PT" => langOptions.Value.Target,
            var t => t[..2],
        };
        this.processInfo = processInfo;
        this.logger = logger;

        if (File.Exists(deeplOptions.Value.GlossaryPath))
        {
            using var reader = new StreamReader(deeplOptions.Value.GlossaryPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false });
            foreach (var (src, dst) in csv.GetRecords<Glossary>())
            {
                this.glossary[src] = dst;
            }
            this.glossaryId = new(RegisterGlossaryAsync);
        }
    }

    private record Glossary(string Source, string Target);

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        var translated = srcTexts.Select(t => t.Text).ToArray();
        this.logger.LogDebug($"Translating {srcTexts.Length} texts.");
        var sw = Stopwatch.StartNew();
        var glossaryId = await this.glossaryId.AsValueTask().ConfigureAwait(false);
        await Parallel.ForEachAsync(srcTexts.GroupBy(t => t.Context).ToAsyncEnumerable(), async (g, ct) =>
        {
            var context = string.Join('\n', this.context, g.Key);
            var srcs = g.Select(t => t.Text).ToArray();
            var results = await translator.TranslateTextAsync(srcs, this.sourceLang, this.targetLang, new() { Context = context, GlossaryId = glossaryId }, ct)
                .ConfigureAwait(false);
            foreach (var (s, t) in srcs.Zip(results))
            {
                var i = Array.IndexOf(translated, s);
                translated[i] = t.Text;
            }
        }).ConfigureAwait(false);
        this.logger.LogDebug($"Translated {srcTexts.Length} texts in {sw.Elapsed}");
        return translated;
    }

    public ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        foreach (var (key, value) in glossary)
        {
            this.glossary.TryAdd(key, value);
        }
        this.glossaryId = new(RegisterGlossaryAsync);
        return default;
    }

    private async ValueTask<string?> RegisterGlossaryAsync()
    {
        var list = await this.translator.ListGlossariesAsync().ConfigureAwait(false);
        if (list.FirstOrDefault(g => g.Name == this.processInfo.Name) is { } exist)
        {
            await this.translator.DeleteGlossaryAsync(exist.GlossaryId).ConfigureAwait(false);
        }
        var created = await this.translator.CreateGlossaryAsync(this.processInfo.Name, this.sourceLang, this.targetLang, new(glossary, true)).ConfigureAwait(false);
        return created.GlossaryId;
    }

    public void RegisterContext(string context)
        => this.context = context;
}

public class DeepLOptions : IPluginParam
{
    [DataType(DataType.Password)]
    [DisplayName("APIキー")]
    public string AuthKey { get; set; } = string.Empty;

    [DisplayName("用語集パス")]
    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "用語集 (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }

    [JsonIgnore]
    [Comment]
    public string Comment { get; } = "Translated by DeepL.(https://www.deepl.com/)";
}


public class DeepLValidator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(DeepLTranslator))
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        // 翻訳言語が対応していないときは無効
        if (settings.Language.Target == "zh-HANT")
        {
            return ValueTask.FromResult(ValidateResult.Invalid("DeepL", "Translation to zh-HANT is currently not supported."));
        }

        // APIキーが設定されている場合は有効
        var op = settings.PluginParams.GetValueOrDefault(nameof(DeepLOptions)) as DeepLOptions;
        if (string.IsNullOrEmpty(op?.AuthKey))
        {
            return ValueTask.FromResult(ValidateResult.Invalid("DeepL", """
            翻訳モジュールにDeepLが選択されています。
            DeepLの利用にはAPIキーが必要です。

            「対象ごとの設定」→「DeepLOptions」タブのAPIキーを設定してください。
            APIキーはDeepLの[アカウントページ](https://www.deepl.com/ja/your-account/keys)から取得できます。

            [こちら](https://youtu.be/D7Yb6rIVPI0)の動画でDeepLのアカウント登録からAPIキーの設定までの手順を解説しています。
            """));
        }

        return ValueTask.FromResult(ValidateResult.Valid);
    }
}