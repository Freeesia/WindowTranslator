using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(TextRect), typeof(int))]
public sealed class AreaToZIndexConverter : IValueConverter
{
    /// <summary>デフォルトインスタンスを取得</summary>
    public static AreaToZIndexConverter Default { get; } = new AreaToZIndexConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TextRect rect)
        {
            return DependencyProperty.UnsetValue;
        }

        // 面積を計算してマイナス値として返す（小さい面積ほど大きなZIndex値になり前面に表示される）
        var area = rect.Width * rect.Height;
        return -(int)area;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}