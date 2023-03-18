using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace WindowTranslator.Data;
public class BoolToDataTemplateConverter : IValueConverter
{
    public DataTemplate? TrueContent { get; set; }
    public DataTemplate? FalseContent { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return (boolValue ? TrueContent : FalseContent) ?? DependencyProperty.UnsetValue;
        }
        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}