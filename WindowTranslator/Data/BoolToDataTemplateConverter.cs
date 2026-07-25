using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(bool), typeof(DataTemplate))]
public sealed class BoolToDataTemplateConverter : IValueConverter
{
    public required DataTemplate FalseContent { get; set; }

    public required DataTemplate TrueContent { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? this.TrueContent : this.FalseContent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
