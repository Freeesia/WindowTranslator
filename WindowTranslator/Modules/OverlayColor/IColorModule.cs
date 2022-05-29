
using System.Windows.Media;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Modules.OverlayColor;
public interface IColorModule
{
    ValueTask<IEnumerable<TextRect>> ConvertColor(SoftwareBitmap bitmap, IEnumerable<TextRect> texts);
}
