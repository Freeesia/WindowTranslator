using System.ComponentModel;

namespace WindowTranslator.Modules.Translate;

[DisplayName("翻訳しない")]
public class NoTranslateModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => ValueTask.FromResult(srcTexts.Select(s => s.Text).ToArray());
}
