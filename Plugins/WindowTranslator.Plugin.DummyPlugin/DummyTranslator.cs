using WindowTranslator.Modules;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.DummyPlugin;

[DisplayName("ダミー")]
public class DummyTranslator : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        throw new NotImplementedException();
    }
}
