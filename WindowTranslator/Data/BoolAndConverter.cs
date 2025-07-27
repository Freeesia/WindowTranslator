using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace WindowTranslator.Data;

[ContentProperty(nameof(Converter))]
public sealed class BoolAndConverter : IMultiValueConverter
{
    public IValueConverter? Converter { get; set; }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var all = values.OfType<bool>().All(b => b);
        if (this.Converter is { } conv)
        {
            return conv.Convert(all, targetType, parameter, culture);
        }
        return all;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
