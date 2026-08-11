#if WINDOWS
using Windows.Graphics.Imaging;

namespace WindowTranslator;

/// <summary>
/// 優先矩形を考慮したテキスト認識を行うユーティリティ
/// </summary>
public static class PriorityRectRecognizer
{
    /// <summary>
    /// 優先度の高い矩形の結果を優先する重なりの割合
    /// </summary>
    private const double OverlapThreshold = 0.5;

    /// <summary>
    /// 優先矩形が登録されている場合は、その矩形内だけを認識する
    /// </summary>
    /// <remarks>
    /// 優先矩形はリストの前方ほど優先度が高く、優先度の高い矩形で文字を認識できた領域と重なった結果は破棄する。
    /// 優先矩形が登録されていない場合だけ、画像全体を認識する。
    /// </remarks>
    /// <param name="bitmap">認識対象の画像</param>
    /// <param name="priorityRects">優先矩形のリスト</param>
    /// <param name="recognizeAsync">
    /// 画像を認識する処理。
    /// 第1引数に認識対象の画像（優先矩形の場合は切り出した画像）、第2引数に元の全体画像を渡す。
    /// 画像全体のサイズを基準にした閾値は第2引数を使うことで、切り出した画像でも全体画像と同じ基準で判定できる。
    /// 結果は第1引数の画像の座標系で返す
    /// </param>
    /// <returns>認識結果</returns>
    public static async ValueTask<IEnumerable<TextRect>> RecognizeAsync(
        SoftwareBitmap bitmap,
        IReadOnlyList<PriorityRect> priorityRects,
        Func<SoftwareBitmap, SoftwareBitmap, ValueTask<IEnumerable<TextRect>>> recognizeAsync)
    {
        if (priorityRects.Count == 0)
        {
            return await recognizeAsync(bitmap, bitmap).ConfigureAwait(false);
        }

        var results = new List<TextRect>();
        // 優先度の高い矩形のうち、文字を認識できた領域
        var recognized = new List<RectInfo>();

        foreach (var priorityRect in priorityRects)
        {
            var absRect = priorityRect.ToAbsoluteRect(bitmap.PixelWidth, bitmap.PixelHeight)
                .Clamp(bitmap.PixelWidth, bitmap.PixelHeight);
            // 1ピクセル未満に潰れた矩形は切り出せないため無視する
            if (absRect.Width < 1 || absRect.Height < 1)
            {
                continue;
            }

            using var cropped = bitmap.Crop(absRect);
            var rectResults = (await recognizeAsync(cropped, bitmap).ConfigureAwait(false))
                // 切り出し位置分オフセットして全体画像の座標系に変換し、キーワードを翻訳コンテキストとして設定する
                .Select(r => r.Offset(absRect.X, absRect.Y, priorityRect.Keyword))
                .Where(r => !IsCoveredBy(r, recognized))
                .ToArray();

            // 何も認識できなかった矩形は、後続の優先矩形の結果を妨げない
            if (rectResults.Length == 0)
            {
                continue;
            }

            results.AddRange(rectResults);
            recognized.Add(absRect);
        }

        return results;
    }

    /// <summary>
    /// 認識結果が優先領域に覆われているかどうかを判定する
    /// </summary>
    /// <remarks>
    /// 複数の優先矩形が重なる場合に、個々の文字ではなく矩形の領域を基準に判定する
    /// </remarks>
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
