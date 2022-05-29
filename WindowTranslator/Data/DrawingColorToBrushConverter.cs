using System.Globalization;
using System.Windows.Data;
using Color = System.Drawing.Color;
using System.Windows;
using System.Windows.Media;

namespace WindowTranslator.Data;

[ValueConversion(typeof(Color), typeof(Brush))]
public sealed class DrawingColorToBrushConverter : IValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static DrawingColorToBrushConverter Default { get; } = new DrawingColorToBrushConverter();

    private readonly Dictionary<Color, Brush> cache = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Color color)
        {
            return DependencyProperty.UnsetValue;
        }
        else if (this.cache.TryGetValue(color, out var brush))
        {
            return brush;
        }
        else
        {
            brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            cache.Add(color, brush);
            return brush;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
