using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

public sealed class TextOverlayVisibilityConverter : IMultiValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static readonly TextOverlayVisibilityConverter Default = new TextOverlayVisibilityConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [TextRect rect, Point pos, double scale, bool isSwap, double padding])
        {
            return Visibility.Visible;
        }
        var r = InflatePadding(new Rect(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale), padding);
        return r.Contains(pos) ^ isSwap ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>
    /// マウスポインター判定の余白（WPF上のピクセル値）を矩形に適用する。
    /// 判定ロジックと設定画面のプレビュー表示で同一の計算を共有するために公開している。
    /// </summary>
    public static Rect InflatePadding(Rect rect, double padding)
    {
        if (padding > 0)
        {
            rect.Inflate(padding, padding);
        }
        return rect;
    }
}
