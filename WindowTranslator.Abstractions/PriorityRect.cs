using System.Drawing;

namespace WindowTranslator;

/// <summary>
/// 優先的にOCRを行う矩形情報
/// </summary>
/// <param name="X">X位置（左上角のX座標、画像幅に対する相対値 0.0-1.0）</param>
/// <param name="Y">Y位置（左上角のY座標、画像高さに対する相対値 0.0-1.0）</param>
/// <param name="Width">幅（画像幅に対する相対値 0.0-1.0）</param>
/// <param name="Height">高さ（画像高さに対する相対値 0.0-1.0）</param>
/// <param name="Keyword">キーワード（翻訳コンテキストに使用）</param>
public record PriorityRect(double X, double Y, double Width, double Height, string Keyword = "")
{
    /// <summary>
    /// 空の優先矩形
    /// </summary>
    public static PriorityRect Empty { get; } = new PriorityRect(0, 0, 0, 0);

    /// <summary>
    /// 絶対座標に変換する
    /// </summary>
    /// <param name="imageWidth">画像の幅</param>
    /// <param name="imageHeight">画像の高さ</param>
    /// <returns>絶対座標の矩形情報</returns>
    public RectInfo ToAbsoluteRect(int imageWidth, int imageHeight)
    {
        return new RectInfo(
            X * imageWidth,
            Y * imageHeight,
            Width * imageWidth,
            Height * imageHeight
        );
    }

    /// <summary>
    /// 絶対座標から相対座標の優先矩形を作成する
    /// </summary>
    /// <param name="x">X位置（絶対座標）</param>
    /// <param name="y">Y位置（絶対座標）</param>
    /// <param name="width">幅（絶対座標）</param>
    /// <param name="height">高さ（絶対座標）</param>
    /// <param name="imageWidth">画像の幅</param>
    /// <param name="imageHeight">画像の高さ</param>
    /// <param name="keyword">キーワード</param>
    /// <returns>相対座標の優先矩形</returns>
    public static PriorityRect FromAbsoluteRect(double x, double y, double width, double height, int imageWidth, int imageHeight, string keyword = "")
    {
        return new PriorityRect(
            x / imageWidth,
            y / imageHeight,
            width / imageWidth,
            height / imageHeight,
            keyword
        );
    }
}
