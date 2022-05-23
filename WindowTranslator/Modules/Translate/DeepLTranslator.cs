using DeepL;

namespace WindowTranslator.Modules.Translate;
public class DeepLTranslator : ITranslateModule
{
    private readonly Translator translator = new(string.Empty);

    public async ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        var translated = await this.translator.TranslateTextAsync(srcTexts, "en", "ja");
        return translated.Select(t => t.Text).ToArray();
    }
}
