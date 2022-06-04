namespace WindowTranslator.Modules.Translate;
public interface ITranslateModule
{
    ValueTask<string[]> TranslateAsync(string[] srcTexts);
}

public class TranslateEmptyModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(string[] srcTexts)
        => ValueTask.FromResult(srcTexts);
}
