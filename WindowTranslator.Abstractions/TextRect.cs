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
    public (double X, double Y, double Width, double Height) GetRotatedBoundingBox()
    {
        if (Math.Abs(Angle) < 1e-10)
        {
            return (X, Y, Width, Height);
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

        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// 他のTextRectと回転を考慮した境界ボックスが重なっているかを判定する
    /// </summary>
    /// <param name="other">比較対象のTextRect</param>
    /// <returns>重なっている場合はtrue、そうでなければfalse</returns>
    public bool OverlapsWith(TextRect other)
    {
        // 自身の回転を考慮した境界ボックスを取得
        var (x1, y1, w1, h1) = GetRotatedBoundingBox();

        // 相手の回転を考慮した境界ボックスを取得
        var (x2, y2, w2, h2) = other.GetRotatedBoundingBox();

        // 矩形の重なり判定
        return !(x1 + w1 <= x2 || x2 + w2 <= x1 || y1 + h1 <= y2 || y2 + h2 <= y1);
    }
};

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