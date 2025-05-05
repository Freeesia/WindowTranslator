using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

[ValueConversion(typeof(FrameworkElement), typeof(double))]
public sealed class SizeToCornerRadiusConverter : IValueConverter
{
    /// <summary>デフォルトインスタンスを取得</summary>
    public static SizeToCornerRadiusConverter Default { get; } = new SizeToCornerRadiusConverter();

    /// <summary>角丸の最大値</summary>
    public double MaxValue { get; set; } = 10;

    /// <summary>角丸の最小値</summary>
    public double MinValue { get; set; } = 2;

    /// <summary>比率感度を調整するためのスケール係数</summary>
    public double ScaleFactor { get; set; } = 0.15;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FrameworkElement element || double.IsNaN(element.MinWidth) || double.IsNaN(element.MinHeight))
        {
            return DependencyProperty.UnsetValue;
        }

        // 計算のために小さい方の寸法を使用
        // TextRectの情報はMinWidthとMinHeightに格納されている
        double smallerDimension = Math.Min(element.MinWidth, element.MinHeight);
        
        // スケール係数を使用して小さい方の寸法に基づいて半径を計算
        double radius = smallerDimension * ScaleFactor;
        
        // 制約を適用
        radius = Math.Max(MinValue, Math.Min(MaxValue, radius));
        
        return radius;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
