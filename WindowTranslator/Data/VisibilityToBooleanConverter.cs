using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(Visibility), typeof(bool))]
public sealed class VisibilityToBooleanConverter : IValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static VisibilityToBooleanConverter Default { get; } = new VisibilityToBooleanConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Hidden;
        }

        return Visibility.Hidden;
    }
}