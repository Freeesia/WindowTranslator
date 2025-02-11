using Windows.Graphics.Imaging;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.OneOcrPlugin;

public class OneOcr : IOcrModule
{
    public ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        throw new NotImplementedException();
    }
}
