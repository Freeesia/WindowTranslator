#if WINDOWS
using Windows.Graphics.Imaging;

namespace WindowTranslator;

/// <summary>
/// 優先矩形を考慮したテキスト認識を行うユーティリティ
/// </summary>
public static class PriorityRectRecognizer
{
    /// <summary>
    /// 優先矩形の結果を優先する重なりの割合
    /// </summary>
    private const double OverlapThreshold = 0.5;

    /// <summary>
    /// 全体の認識結果と優先矩形の認識結果をマージする
    /// </summary>
    /// <remarks>
    /// 優先矩形はリストの前方ほど優先度が高く、優先度の高い矩形の結果と重なった結果は破棄する
    /// </remarks>
    /// <param name="bitmap">認識対象の画像</param>
    /// <param name="priorityRects">優先矩形のリスト</param>
    /// <param name="recognizeAsync">画像全体を認識する処理（元画像の座標系で結果を返す）</param>
    /// <returns>認識結果</returns>
    public static async ValueTask<IEnumerable<TextRect>> RecognizeAsync(
        SoftwareBitmap bitmap,
        IReadOnlyList<PriorityRect> priorityRects,
        Func<SoftwareBitmap, ValueTask<IEnumerable<TextRect>>> recognizeAsync)
    {
        if (priorityRects.Count == 0)
        {
            return await recognizeAsync(bitmap).ConfigureAwait(false);
        }

        var results = new List<TextRect>();
        // 認識済みの優先矩形（前方の矩形ほど優先度が高い）
        var recognized = new List<RectInfo>(priorityRects.Count);

        foreach (var priorityRect in priorityRects)
        {
            var absRect = priorityRect.ToAbsoluteRect(bitmap.PixelWidth, bitmap.PixelHeight)
                .Clamp(bitmap.PixelWidth, bitmap.PixelHeight);
            if (absRect.IsEmpty)
            {
                continue;
            }

            using var cropped = bitmap.Crop(absRect);
            var rectResults = await recognizeAsync(cropped).ConfigureAwait(false);

            // 切り出し位置分オフセットして全体画像の座標系に変換し、キーワードを翻訳コンテキストとして設定する
            results.AddRange(rectResults
                .Select(r => r.Offset(absRect.X, absRect.Y, priorityRect.Keyword))
                .Where(r => !IsCoveredBy(r, recognized)));
            recognized.Add(absRect);
        }

        // 全体の認識結果のうち、優先矩形で認識済みの領域と重なるものは破棄する
        var fullResults = await recognizeAsync(bitmap).ConfigureAwait(false);
        results.AddRange(fullResults.Where(r => !IsCoveredBy(r, recognized)));

        return results;
    }

    private static bool IsCoveredBy(TextRect text, List<RectInfo> areas)
    {
        if (areas.Count == 0)
        {
            return false;
        }
        var box = text.GetRotatedBoundingBox();
        return areas.Any(a => a.IntersectionRatio(box) >= OverlapThreshold);
    }
}
#endif
