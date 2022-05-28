using Windows.Graphics.Imaging;

namespace WindowTranslator.Modules.Ocr;
public interface IOcrModule
{
    ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap);
}
