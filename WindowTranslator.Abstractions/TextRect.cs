using System.Drawing;

namespace WindowTranslator;

/// <summary>
/// 翻訳テキストの矩形情報
/// </summary>
/// <param name="Text">
/// テキスト
/// <paramref name="IsTranslated"/> が true の場合は翻訳後のテキスト
/// </param>
/// <param name="X">X位置</param>
/// <param name="Y">Y位置</param>
/// <param name="Width">幅</param>
/// <param name="Height">高さ</param>
/// <param name="FontSize">フォントサイズ</param>
/// <param name="Line">表示可能行数</param>
/// <param name="Foreground">文字色</param>
/// <param name="Background">背景色</param>
/// <param name="IsTranslated"><paramref name="Text"/>が翻訳後のテキストかどうか</param>
public record TextRect(string Text, double X, double Y, double Width, double Height, double FontSize, int Line, Color Foreground, Color Background, bool IsTranslated = false)
{
    /// <summary>
    /// 空
    /// </summary>
    public static TextRect Empty { get; } = new TextRect(string.Empty, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="text">テキスト</param>
    /// <param name="x">X位置</param>
    /// <param name="y">Y位置</param>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    /// <param name="fontSize">フォントサイズ</param>
    /// <param name="line">表示可能行数</param>
    public TextRect(string text, double x, double y, double width, double height, double fontSize, int line)
        : this(text, x, y, width, height, fontSize, line, Color.Red, Color.WhiteSmoke)
    {
    }
};