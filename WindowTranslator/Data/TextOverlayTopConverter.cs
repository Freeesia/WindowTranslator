using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(TextRect), typeof(double))]
public sealed class TextOverlayTopConverter : IValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static TextOverlayTopConverter Default { get; } = new TextOverlayTopConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TextRect rect)
        {
            return DependencyProperty.UnsetValue;
        }
        return rect.Y - (rect.FontSize * 0.25);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
