using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

/// <summary>
/// マウスポインター判定余白のプレビュー矩形の一辺の長さを計算する。
/// <see cref="TextOverlayVisibilityConverter.InflatePadding(Rect, double)"/>と同じ計算を利用することで、
/// 実際の当たり判定と表示される範囲を完全に一致させている。
/// </summary>
public sealed class PreviewHitTestSizeConverter : IValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static readonly PreviewHitTestSizeConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double padding)
        {
            return 0.0;
        }
        return TextOverlayVisibilityConverter.InflatePadding(new Rect(0, 0, 0, 0), padding).Width;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
