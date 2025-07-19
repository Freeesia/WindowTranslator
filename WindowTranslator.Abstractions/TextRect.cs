using System.Drawing;

namespace WindowTranslator;

/// <summary>
/// 翻訳テキストの矩形情報
/// </summary>
/// <param name="SourceText">翻訳前テキスト</param>
/// <param name="X">X位置（左上角のX座標）</param>
/// <param name="Y">Y位置（左上角のY座標）</param>
/// <param name="Width">幅</param>
/// <param name="Height">高さ</param>
/// <param name="FontSize">フォントサイズ</param>
/// <param name="MultiLine">複数行かどうか</param>
/// <param name="Foreground">文字色</param>
/// <param name="Background">背景色</param>
public record TextRect(string SourceText, double X, double Y, double Width, double Height, double FontSize, bool MultiLine, Color Foreground, Color Background)
    : TextInfo(SourceText, null)
{
    /// <summary>
    /// 空
    /// </summary>
    public static TextRect Empty { get; } = new TextRect(string.Empty, 0, 0, 0, 0, 0, false);

    /// <summary>
    /// 左上を中心とした回転角度（度数法、時計回り）
    /// </summary>
    public double Angle { get; init; }

    /// <summary>
    /// 最大幅
    /// </summary>
    public double MaxWidth { get; init; } = double.NaN;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="text">テキスト</param>
    /// <param name="x">X位置</param>
    /// <param name="y">Y位置</param>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    /// <param name="fontSize">フォントサイズ</param>
    /// <param name="multiLine">複数行かどうか</param>
    public TextRect(string text, double x, double y, double width, double height, double fontSize, bool multiLine)
        : this(text, x, y, width, height, fontSize, multiLine, Color.Red, Color.WhiteSmoke)
    {
    }

    /// <summary>
    /// 回転を考慮した境界ボックスを計算する
    /// </summary>
    /// <returns>回転を考慮した境界ボックス (X, Y, Width, Height)</returns>
    public RectInfo GetRotatedBoundingBox()
    {
        if (Math.Abs(Angle) < 1e-10)
        {
            return new(X, Y, Width, Height);
        }

        var angleRadians = Angle * Math.PI / 180.0;
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);
        var centerX = X;
        var centerY = Y;

        Span<(double x, double y)> corners =
        [
            (0.0, 0.0), // 左上（回転中心）
            (Width, 0.0), // 右上
            (Width, Height), // 右下
            (0.0, Height), // 左下
        ];

        for (var i = 0; i < corners.Length; i++)
        {
            var (x, y) = corners[i];
            corners[i] = (x * cos - y * sin + centerX, x * sin + y * cos + centerY);
        }

        var minX = corners[0].x;
        var maxX = corners[0].x;
        var minY = corners[0].y;
        var maxY = corners[0].y;

        for (var i = 1; i < corners.Length; i++)
        {
            var (x, y) = corners[i];
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        return new(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// 他のTextRectと回転を考慮した境界ボックスが重なっているかを判定する
    /// </summary>
    /// <param name="other">比較対象のTextRect</param>
    /// <returns>重なっている場合はtrue、そうでなければfalse</returns>
    public bool OverlapsWith(TextRect other)
        => GetRotatedBoundingBox().OverlapsWith(other.GetRotatedBoundingBox());

/// <summary>
/// 矩形情報
/// </summary>
/// <param name="X">X位置（左上角のX座標）</param>
/// <param name="Y">Y位置（左上角のY座標）</param>
/// <param name="Width">幅</param>
/// <param name="Height">高さ</param>
public readonly record struct RectInfo(double X, double Y, double Width, double Height)
{
    /// <summary>
    /// 空の矩形
    /// </summary>
    public static readonly RectInfo Empty = new(0, 0, 0, 0);

    /// <summary>
    /// 矩形が空かどうか
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// 矩形の上端のY座標
    /// </summary>
    public double Top => Y;

    /// <summary>
    /// 矩形の下端のY座標
    /// </summary>
    public double Bottom => Y + Height;

    /// <summary>
    /// 矩形の左端のX座標
    /// </summary>
    public double Left => X;

    /// <summary>
    /// 矩形の右端のX座標
    /// </summary>
    public double Right => X + Width;

    /// <summary>
    /// 矩形の重なり判定
    /// </summary>
    /// <param name="other">比較対象</param>
    /// <returns>重なっている場合はtrue、そうでなければfalse</returns>
    public bool OverlapsWith(RectInfo other) =>
        !(Right <= other.Left || other.Right <= Left || Bottom <= other.Top || other.Bottom <= Top);
}

/// <summary>
/// 翻訳テキストの矩形情報
/// </summary>
/// <param name="SourceText">翻訳前のテキスト</param>
/// <param name="TranslatedText">翻訳後のテキスト</param>
public record TextInfo(string SourceText, string? TranslatedText)
{
    /// <summary>
    /// このテキストの文脈
    /// </summary>
    public string Context { get; init; } = string.Empty;
};