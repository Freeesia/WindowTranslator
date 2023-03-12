using System.Globalization;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(double), typeof(double))]
public sealed class TextOverlayTransformYConverter : IValueConverter
{
    public static readonly TextOverlayTransformYConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var size = (double)value;
        var rate = double.Parse(parameter.ToString() ?? "1");
        return size * rate;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
