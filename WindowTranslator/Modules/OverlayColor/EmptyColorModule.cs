using System.Drawing;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Modules.OverlayColor;

public class EmptyColorModule : IColorModule
{
    public ValueTask<IEnumerable<TextRect>> ConvertColorAsync(SoftwareBitmap bitmap, IEnumerable<TextRect> texts)
        => new(texts);
}

public class TransparentColorModule : IColorModule
{
    public ValueTask<IEnumerable<TextRect>> ConvertColorAsync(SoftwareBitmap bitmap, IEnumerable<TextRect> texts)
        => new(texts.Select(t => t with { Background = Color.Transparent, Foreground = Color.Transparent }));
}
