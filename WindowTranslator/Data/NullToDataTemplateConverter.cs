using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace WindowTranslator.Data;

[ValueConversion(typeof(bool), typeof(DataTemplate))]
public class NullToDataTemplateConverter : IValueConverter
{
    public required DataTemplate NullContent { get; set; }
    public required DataTemplate NotNullContent { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? NullContent : NotNullContent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}