using System.Text.RegularExpressions;

namespace WindowTranslator;

/// <summary>
/// OCRに関連するユーティリティメソッドを提供します。
/// </summary>
public static partial class OcrUtility
{
    /// <summary>
    /// 元画像基準の相対閾値を、OCRに渡すスケール後画像のピクセル値へ変換する。
    /// </summary>
    /// <param name="relativeThreshold">元画像サイズに対する相対閾値</param>
    /// <param name="sourcePixels">元画像のピクセル数</param>
    /// <param name="scale">OCR前に適用する拡大率</param>
    /// <returns>スケール後画像の座標系における閾値</returns>
    public static double ToScaledThreshold(double relativeThreshold, int sourcePixels, double scale)
        => relativeThreshold * sourcePixels * scale;

    [GeneratedRegex(@"^[\s\p{S}\p{P}\d]+$")]
    public static partial Regex AllSymbolOrSpace();
}
