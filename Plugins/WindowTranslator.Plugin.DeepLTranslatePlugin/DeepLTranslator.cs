using DeepL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropertyTools.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Serialization;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Stores;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.DeepLTranslatePlugin;

[DefaultModule]
[DisplayName("DeepL")]
public class DeepLTranslator(
    IProcessInfoStore processInfo,
    IOptionsSnapshot<DeepLOptions> deeplOptions,
    IOptionsSnapshot<LanguageOptions> langOptions,
    ILogger<DeepLTranslator> logger) : ITranslateModule
{
    private readonly Translator translator = new(deeplOptions.Value.AuthKey);
    private readonly string sourceLang = langOptions.Value.Source[..2];
    private readonly string targetLang = langOptions.Value.Target switch
    {
        "en-US" or "en-GB" or "pt-BR" or "pt-PT" => langOptions.Value.Target,
        var t => t[..2],
    };
    private readonly IProcessInfoStore processInfo = processInfo;
    private readonly ILogger<DeepLTranslator> logger = logger;
    private string? glossaryId = null;
    private string? context = null;

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        var translated = srcTexts.Select(t => t.Text).ToArray();
        this.logger.LogDebug($"Translating {srcTexts.Length} texts.");
        var sw = Stopwatch.StartNew();
        await Parallel.ForEachAsync(srcTexts.GroupBy(t => t.Context).ToAsyncEnumerable(), async (g, ct) =>
        {
            var context = string.Join('\n', this.context, g.Key);
            var srcs = g.Select(t => t.Text).ToArray();
            var results = await translator.TranslateTextAsync(srcs, this.sourceLang, this.targetLang, new() { Context = context, GlossaryId = this.glossaryId }, ct)
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

    public async ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        var list = await this.translator.ListGlossariesAsync().ConfigureAwait(false);
        if (list.FirstOrDefault(g => g.Name == this.processInfo.Name) is { } exist)
        {
            await this.translator.DeleteGlossaryAsync(exist.GlossaryId).ConfigureAwait(false);
        }
        var created = await this.translator.CreateGlossaryAsync(this.processInfo.Name, this.sourceLang, this.targetLang, new(glossary, true)).ConfigureAwait(false);
        this.glossaryId = created.GlossaryId;
    }

    public void RegisterContext(string context)
        => this.context = context;
}

public class DeepLOptions : IPluginParam
{
    [DataType(DataType.Password)]
    [DisplayName("APIキー")]
    public string AuthKey { get; set; } = string.Empty;

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