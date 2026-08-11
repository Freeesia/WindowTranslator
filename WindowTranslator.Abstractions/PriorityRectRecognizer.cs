#if WINDOWS
using Windows.Graphics.Imaging;

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
    /// OCR対象範囲はリストの前方ほど優先度が高く、優先度の高い矩形で文字を認識できた領域と重なった結果は破棄する。
    /// OCR対象範囲が登録されていない場合だけ、画像全体を認識する。
    /// </remarks>
    /// <param name="bitmap">認識対象の画像</param>
    /// <param name="priorityRects">OCR対象範囲のリスト</param>
    /// <param name="recognizeAsync">
    /// 画像を認識する処理。
    /// 第1引数に認識対象の画像（OCR対象範囲の場合は切り出した画像）、第2引数に元の全体画像を渡す。
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
        // 優先度の高い矩形で採用した認識結果
        var recognized = new List<TextRect>();

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

            // 何も認識できなかった矩形は、後続のOCR対象範囲の結果を妨げない
            if (rectResults.Length == 0)
            {
                continue;
            }

            results.AddRange(rectResults);
            recognized.AddRange(rectResults);
        }

        return results;
    }

    /// <summary>
    /// 認識結果が優先度の高い認識結果に覆われているかどうかを判定する
    /// </summary>
    /// <remarks>
    /// 複数のOCR対象範囲が重なる場合でも、実際の認識結果同士が重なる場合だけ低優先度側を破棄する
    /// </remarks>
    private static bool IsCoveredBy(TextRect text, List<TextRect> recognized)
    {
        if (recognized.Count == 0)
        {
            return false;
        }
        var box = text.GetRotatedBoundingBox();
        return recognized.Any(r => r.GetRotatedBoundingBox().IntersectionRatio(box) >= OverlapThreshold);
    }
}
#endif
