namespace WindowTranslator.Modules;
public interface ITranslateModule
{
    ValueTask<string[]> TranslateAsync(string[] srcTexts);
}