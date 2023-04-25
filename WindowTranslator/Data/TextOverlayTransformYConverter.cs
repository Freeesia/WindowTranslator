using System.Globalization;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(double), typeof(double))]
public sealed class TextOverlayTransformYConverter : IValueConverter
{
    public static readonly TextOverlayTransformYConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double size)
        {
            var rate = double.Parse(parameter.ToString() ?? "1", CultureInfo.InvariantCulture);
            return size * rate;
        }
        return .0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
