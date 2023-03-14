using DeepL;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.DeepLTranslatePlugin;

[DisplayName("DeepL翻訳")]
public class DeepLTranslator : ITranslateModule
{
    private readonly Translator translator;
    private readonly string sourceLang;
    private readonly string targetLang;

    public DeepLTranslator(IPluginOptions<DeepLOptions> deeplOptions, IOptionsSnapshot<LanguageOptions> langOptions)
    {
        this.translator = new(deeplOptions.Param.AuthKey, deeplOptions.Param.Options);
        this.sourceLang = langOptions.Value.Source[..2];
        this.targetLang = langOptions.Value.Target switch
        {
            "en-US" or "en-GB" or "pt-BR" or "pt-PT" => langOptions.Value.Target,
            var t => t[..2],
        };
    }

    public async ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        var translated = await translator.TranslateTextAsync(srcTexts, this.sourceLang, this.targetLang);
        return translated.Select(t => t.Text).ToArray();
    }
}

public class DeepLOptions
{
    public string AuthKey { get; set; } = string.Empty;
    public TranslatorOptions? Options { get; set; }
}
