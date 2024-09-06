using WindowTranslator.Modules;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.DummyPlugin;

[DisplayName("ダミー")]
public class DummyTranslator : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(string[] srcTexts)
        => ValueTask.FromResult(srcTexts);
}

[DisplayName("空文字化")]
public class TranslateEmptyModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(string[] srcTexts)
        => ValueTask.FromResult((string[])Array.CreateInstance(typeof(string), srcTexts.Length));
}
