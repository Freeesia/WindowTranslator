using WindowTranslator.ComponentModel;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.Translate;

[LocalizedDisplayName(typeof(Resources), nameof(NoTranslateModule))]
public class NoTranslateModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => ValueTask.FromResult(srcTexts.Select(s => s.Text).ToArray());
}
