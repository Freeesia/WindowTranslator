using System.Globalization;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBoolConverter : IValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static readonly InverseBoolConverter Default = new ();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return value;
    }
}
