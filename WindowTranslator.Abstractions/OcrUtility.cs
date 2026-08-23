using System.Text.RegularExpressions;

namespace WindowTranslator;

/// <summary>
/// OCRに関連するユーティリティメソッドを提供します。
/// </summary>
public static partial class OcrUtility
{
#if WINDOWS
    private const double OverlapThreshold = 0.5;

    /// <summary>
    /// 1回のキャプチャーに含まれるOCR対象範囲を順番に切り出して認識する
    /// </summary>
    /// <param name="input">全体画像とOCR対象範囲</param>
    /// <param name="recognizeAsync">切り出した画像を認識する処理</param>
    /// <returns>全体画像の座標系へ変換し、優先度の重複を除外した認識結果</returns>
    public static async ValueTask<IReadOnlyList<TextRect>> RecognizeRegionsAsync(
        Modules.OcrCaptureInput input,
        Func<Windows.Graphics.Imaging.SoftwareBitmap, ValueTask<IReadOnlyList<TextRect>>> recognizeAsync)
    {
        var results = new List<TextRect>();

        foreach (var region in input.Regions)
        {
            var bounds = region.Bounds;
            var isSource = bounds.X == 0
                && bounds.Y == 0
                && bounds.Width == input.Source.PixelWidth
                && bounds.Height == input.Source.PixelHeight;
            var bitmap = isSource
                ? input.Source
                : input.Source.Crop((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);

            try
            {
                var regionResults = await recognizeAsync(bitmap).ConfigureAwait(false);
                var offsetResults = regionResults
                    .Select(r => r.Offset(bounds.X, bounds.Y, region.Keyword))
                    .Where(r => !IsCoveredBy(r, results))
                    .ToArray();
                results.AddRange(offsetResults);
            }
            finally
            {
                if (!isSource)
                {
                    bitmap.Dispose();
                }
            }
        }

        return results;
    }

    private static bool IsCoveredBy(TextRect text, List<TextRect> recognized)
    {
        var box = text.GetRotatedBoundingBox();
        return recognized.Any(r => IntersectionRatio(r.GetRotatedBoundingBox(), box) >= OverlapThreshold);
    }

    private static double IntersectionRatio(RectInfo area, RectInfo other)
    {
        var width = Math.Max(0, Math.Min(area.Right, other.Right) - Math.Max(area.Left, other.Left));
        var height = Math.Max(0, Math.Min(area.Bottom, other.Bottom) - Math.Max(area.Top, other.Top));
        return width * height / (other.Width * other.Height);
    }
#endif

    [GeneratedRegex(@"^[\s\p{S}\p{P}\d]+$")]
    public static partial Regex AllSymbolOrSpace();
}
