#if WINDOWS
using Windows.Graphics.Imaging;
using WindowTranslator.Modules;

namespace WindowTranslator;

/// <summary>
/// 指定されたOCR対象範囲だけをテキスト認識するユーティリティ
/// </summary>
public static class PriorityRectRecognizer
{
    /// <summary>
    /// 優先度の高い矩形の結果を優先する重なりの割合
    /// </summary>
    private const double OverlapThreshold = 0.5;

    /// <summary>
    /// OCR対象範囲が登録されている場合は、その矩形内だけを認識する
    /// </summary>
    /// <remarks>
    /// OCR対象範囲はリストの前方ほど優先度が高い。
    /// 低優先度側の認識結果の面積に対する重なりが50%以上の場合、その結果を破棄する。
    /// OCR対象範囲が登録されていない場合だけ、画像全体を認識する。
    /// </remarks>
    /// <param name="bitmap">認識対象の画像</param>
    /// <param name="priorityRects">OCR対象範囲のリスト</param>
    /// <param name="recognizeAsync">1回のキャプチャーに含まれるOCR対象画像をまとめて認識する処理</param>
    /// <returns>認識結果</returns>
    public static async ValueTask<IReadOnlyList<TextRect>> RecognizeAsync(
        SoftwareBitmap bitmap,
        IReadOnlyList<PriorityRect> priorityRects,
        Func<OcrCaptureInput, ValueTask<IReadOnlyList<IReadOnlyList<TextRect>>>> recognizeAsync)
    {
        if (priorityRects.Count == 0)
        {
            var fullResults = await recognizeAsync(
                new(bitmap, [new(new(0, 0, bitmap.PixelWidth, bitmap.PixelHeight))]))
                .ConfigureAwait(false);
            return fullResults.SelectMany(r => r).ToArray();
        }

        var regions = new List<OcrRegionInput>();
        var rects = new List<(RectInfo CropRect, string Keyword)>();
        foreach (var priorityRect in priorityRects)
        {
            var absRect = priorityRect.ToAbsoluteRect(bitmap.PixelWidth, bitmap.PixelHeight)
                .ClampToImage(bitmap.PixelWidth, bitmap.PixelHeight);
            // 1ピクセル未満に潰れた矩形は切り出せないため無視する
            if (absRect.Width < 1 || absRect.Height < 1)
            {
                continue;
            }

            var cropRect = absRect.ToPixelRect();
            regions.Add(new(cropRect));
            rects.Add((cropRect, priorityRect.Keyword));
        }

        if (regions.Count == 0)
        {
            return [];
        }

        var captureResults = await recognizeAsync(new(bitmap, regions)).ConfigureAwait(false);
        var results = new List<TextRect>();

        foreach (var (regionResults, rect) in captureResults.Zip(rects))
        {
            var (cropRect, keyword) = rect;
            var offsetResults = regionResults
                // 切り出し位置分オフセットして全体画像の座標系に変換し、キーワードを翻訳コンテキストとして設定する
                .Select(r => r.Offset(cropRect.X, cropRect.Y, keyword))
                .Where(r => !IsCoveredBy(r, results))
                .ToArray();

            results.AddRange(offsetResults);
        }

        return results;
    }

    /// <summary>
    /// 認識結果が優先度の高い認識結果に覆われているかどうかを判定する
    /// </summary>
    /// <remarks>
    /// 複数のOCR対象範囲が重なる場合でも、低優先度側の認識結果の面積の50%以上が重なる場合だけ破棄する
    /// </remarks>
    private static bool IsCoveredBy(TextRect text, List<TextRect> recognized)
    {
        var box = text.GetRotatedBoundingBox();
        return recognized.Any(r => IntersectionRatio(r.GetRotatedBoundingBox(), box) >= OverlapThreshold);
    }

    /// <summary>
    /// 指定した画像内に収まるように矩形を切り詰める
    /// </summary>
    private static RectInfo ClampToImage(this RectInfo rect, int imageWidth, int imageHeight)
    {
        var left = Math.Clamp(rect.Left, 0, imageWidth);
        var top = Math.Clamp(rect.Top, 0, imageHeight);
        var right = Math.Clamp(rect.Right, 0, imageWidth);
        var bottom = Math.Clamp(rect.Bottom, 0, imageHeight);
        return new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>
    /// 矩形と交差するすべてのピクセルを含む整数座標へ変換する
    /// </summary>
    private static RectInfo ToPixelRect(this RectInfo rect)
    {
        var left = Math.Floor(rect.Left);
        var top = Math.Floor(rect.Top);
        var right = Math.Ceiling(rect.Right);
        var bottom = Math.Ceiling(rect.Bottom);
        return new(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// <paramref name="other"/>の面積に対する重なり部分の割合を計算する
    /// </summary>
    private static double IntersectionRatio(RectInfo area, RectInfo other)
    {
        var width = Math.Max(0, Math.Min(area.Right, other.Right) - Math.Max(area.Left, other.Left));
        var height = Math.Max(0, Math.Min(area.Bottom, other.Bottom) - Math.Max(area.Top, other.Top));
        return width * height / (other.Width * other.Height);
    }
}
#endif
