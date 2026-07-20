using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Data;

/// <summary>
/// マウスポインター判定余白のプレビュー矩形の表示可否を判定する。
/// 値がnull（設定コントロールにフォーカスがない）の場合は非表示にする。
/// </summary>
public sealed class NullableToVisibilityConverter : IValueConverter
{
    /// <summary> Gets the default instance </summary>
    public static readonly NullableToVisibilityConverter Default = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
