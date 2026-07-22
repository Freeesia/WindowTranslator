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
        var r = new Rect(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
        return HitTest(r, pos, padding) ^ isSwap ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    /// <summary>
    /// 矩形と、その周囲に加えたマウスポインター判定の余白（WPF上のピクセル値）を対象に、
    /// 位置が範囲内かどうかを判定する。余白部分は矩形からの最短距離を基準にした円形
    /// （角丸/スタジアム形状）で判定するため、四隅がpadding分だけ直角に広がることはない。
    /// 判定ロジックと設定画面のプレビュー表示で同一の計算を共有するために公開している。
    /// </summary>
    public static bool HitTest(Rect rect, Point pos, double padding)
    {
        if (rect.Contains(pos))
        {
            return true;
        }
        if (padding <= 0)
        {
            return false;
        }
        var dx = Math.Max(0, Math.Max(rect.Left - pos.X, pos.X - rect.Right));
        var dy = Math.Max(0, Math.Max(rect.Top - pos.Y, pos.Y - rect.Bottom));
        return (dx * dx) + (dy * dy) <= padding * padding;
    }
}
