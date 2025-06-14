namespace WindowTranslator.Modules.Translate;

public class NoTranslateModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => ValueTask.FromResult(srcTexts.Select(s => s.Text).ToArray());
}
