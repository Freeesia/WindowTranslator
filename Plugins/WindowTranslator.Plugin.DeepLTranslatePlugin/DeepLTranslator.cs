using DeepL;
using Microsoft.Extensions.Options;
using PropertyTools.DataAnnotations;
using System.ComponentModel;
using System.Text.Json.Serialization;
using WindowTranslator.Modules;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.DeepLTranslatePlugin;

[DisplayName("DeepL")]
public class DeepLTranslator : ITranslateModule
{
    private readonly Translator translator;
    private readonly string sourceLang;
    private readonly string targetLang;

    public DeepLTranslator(IOptionsSnapshot<DeepLOptions> deeplOptions, IOptionsSnapshot<LanguageOptions> langOptions)
    {
        this.translator = new(deeplOptions.Value.AuthKey, deeplOptions.Value.Options);
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

public class DeepLOptions : IPluginParam
{
    public string AuthKey { get; set; } = string.Empty;
    public TranslatorOptions? Options { get; set; }

    [JsonIgnore]
    [Comment]
    public string Comment { get; } = "Translated by DeepL.(https://www.deepl.com/)";
}
