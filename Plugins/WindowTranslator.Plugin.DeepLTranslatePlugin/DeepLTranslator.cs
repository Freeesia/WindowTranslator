using DeepL;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.DeepLTranslatePlugin;
public class DeepLTranslator : ITranslateModule
{
    private readonly Translator translator;
    private readonly LanguageOptions langOptions;

    public DeepLTranslator(IPluginOptions<DeepLOptions> deeplOptions, IOptionsSnapshot<LanguageOptions> langOptions)
    {
        this.translator = new(deeplOptions.Param.AuthKey, deeplOptions.Param.Options);
        this.langOptions = langOptions.Value;
    }

    public async ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        var translated = await translator.TranslateTextAsync(srcTexts, this.langOptions.Source, this.langOptions.Target);
        return translated.Select(t => t.Text).ToArray();
    }
}

public class DeepLOptions
{
    public string AuthKey { get; set; } = string.Empty;
    public TranslatorOptions? Options { get; set; }
}
