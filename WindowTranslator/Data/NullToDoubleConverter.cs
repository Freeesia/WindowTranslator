using System.Globalization;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(object), typeof(double))]
public class NullToDoubleConverter : IValueConverter
{
    public double NullValue { get; set; }
    public double NotNullValue { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? NullValue : NotNullValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}