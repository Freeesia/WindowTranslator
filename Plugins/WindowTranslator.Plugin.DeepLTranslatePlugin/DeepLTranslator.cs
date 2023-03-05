using DeepL;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.DeepLTranslatePlugin;
public class DeepLTranslator : ITranslateModule
{
    private readonly Translator translator;

    public DeepLTranslator(IPluginOptions<DeepLOptions> options)
        => translator = new(options.Param.AuthKey, options.Param.Options);

    public async ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        var translated = await translator.TranslateTextAsync(srcTexts, "en", "ja");
        return translated.Select(t => t.Text).ToArray();
    }
}

public class DeepLOptions
{
    public string AuthKey { get; set; } = string.Empty;
    public TranslatorOptions? Options { get; set; }
}
