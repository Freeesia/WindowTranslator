using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace WindowTranslator.Modules.Ocr;
public class WindowsMediaOcr : IOcrModule
{
    private readonly OcrEngine ocr = OcrEngine.TryCreateFromLanguage(new("en-US"));
    public async ValueTask<TextResult[]> RecognizeAsync(SoftwareBitmap bitmap)
    {
        var result = await ocr.RecognizeAsync(bitmap);
        return result
            .Lines
            .Select(l => new TextResult(
                l.Text,
                l.Words.Select(w => w.BoundingRect.X).Min(),
                l.Words.Select(w => w.BoundingRect.Y).Min(),
                l.Words.Select(w => w.BoundingRect.Width).Max(),
                l.Words.Select(w => w.BoundingRect.Height).Max()))
            // 大きすぎる文字は映像の認識ミスとみなす
            .Where(w => w.Height < bitmap.PixelHeight * 0.1)
            // 少なすぎる文字も認識ミス扱い
            .Where(w => w.Text.Length > 2)
            .ToArray();
    }
}
