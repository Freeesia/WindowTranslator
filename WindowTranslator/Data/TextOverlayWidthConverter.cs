using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(TextRect), typeof(double))]
public sealed class TextOverlayWidthConverter : IValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static TextOverlayWidthConverter Default { get; } = new TextOverlayWidthConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TextRect rect)
        {
            return DependencyProperty.UnsetValue;
        }
        // 複数行の時は改行して収まるようにする
        if (rect.Line > 1)
        {
            return rect.Width * 1.01;
        }
        // 1行の時ははみ出ても良いことにする
        return double.NaN;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
