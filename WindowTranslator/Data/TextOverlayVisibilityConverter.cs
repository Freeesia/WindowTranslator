using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

public sealed class TextOverlayVisibilityConverter : IMultiValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static readonly TextOverlayVisibilityConverter Default = new TextOverlayVisibilityConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [TextRect rect, Point pos, double scale])
        {
            return Visibility.Visible;
        }
        var r = new Rect(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
        return r.Contains(pos) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
