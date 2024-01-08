using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BooleanToVisibilityConverter : IValueConverter
{
    public Visibility True { get; set; }
    public Visibility False { get; set; }
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not bool v)
        {
            return DependencyProperty.UnsetValue;
        }
        return v ? this.True : this.False;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
