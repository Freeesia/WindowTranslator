namespace WindowTranslator.Modules.Translate;
public interface ITranslateModule
{
    ValueTask<string[]> TranslateAsync(string[] srcTexts);
}
