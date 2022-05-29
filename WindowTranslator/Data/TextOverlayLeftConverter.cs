using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(TextRect), typeof(double))]
public sealed class TextOverlayLeftConverter : IValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static TextOverlayLeftConverter Default { get; } = new TextOverlayLeftConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TextRect rect)
        {
            return DependencyProperty.UnsetValue;
        }
        return rect.X;
        //return rect.X - (rect.FontSize * 0.1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
