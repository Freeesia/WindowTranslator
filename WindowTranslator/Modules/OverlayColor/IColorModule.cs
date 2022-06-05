using Windows.Graphics.Imaging;

namespace WindowTranslator.Modules.OverlayColor;
public interface IColorModule
{
    ValueTask<IEnumerable<TextRect>> ConvertColorAsync(SoftwareBitmap bitmap, IEnumerable<TextRect> texts);
}
