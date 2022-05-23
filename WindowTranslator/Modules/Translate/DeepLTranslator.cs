using DeepL;
using Microsoft.Extensions.Options;

namespace WindowTranslator.Modules.Translate;
public class DeepLTranslator : ITranslateModule
{
    private readonly Translator translator;

    public DeepLTranslator(IOptions<DeepLOptions> options)
        => this.translator = new(options.Value.AuthKey, options.Value.Options);

    public async ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        var translated = await this.translator.TranslateTextAsync(srcTexts, "en", "ja");
        return translated.Select(t => t.Text).ToArray();
    }
}

public class DeepLOptions
{
    public string AuthKey { get; set; } = string.Empty;
    public TranslatorOptions? Options { get; set; }
}
