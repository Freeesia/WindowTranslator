using WindowTranslator.Modules;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.DummyPlugin;

[DisplayName("空文字化")]
public class TranslateEmptyModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => ValueTask.FromResult((string[])Array.CreateInstance(typeof(string), srcTexts.Length));
}


[DisplayName("完了しない")]
public class TranslateInfinityModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => new(Task.Delay(-1).ContinueWith(_ => (string[])Array.CreateInstance(typeof(string), srcTexts.Length)));
}

[DisplayName("翻訳しない")]
public class NoTranslateModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => ValueTask.FromResult(srcTexts.Select(s => s.Text).ToArray());
}
