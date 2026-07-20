using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

/// <summary>
/// マウス位置と判定余白から、プレビュー矩形のMargin（左上位置）を計算する。
/// <see cref="TextOverlayVisibilityConverter.InflatePadding(Rect, double)"/>と同じ計算を利用することで、
/// 実際の当たり判定と表示される範囲を完全に一致させている。
/// </summary>
public sealed class PreviewHitTestMarginConverter : IMultiValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static readonly PreviewHitTestMarginConverter Default = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [Point pos, double padding])
        {
            return new Thickness(0);
        }
        var r = TextOverlayVisibilityConverter.InflatePadding(new Rect(pos, new Size(0, 0)), padding);
        return new Thickness(r.X, r.Y, 0, 0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
