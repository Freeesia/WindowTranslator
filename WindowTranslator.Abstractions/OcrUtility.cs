using System.Text.RegularExpressions;

namespace WindowTranslator;

/// <summary>
/// OCRに関連するユーティリティメソッドを提供します。
/// </summary>
public static partial class OcrUtility
{
#if WINDOWS
    /// <summary>
    /// 1回のキャプチャーに含まれるOCR対象範囲を順番に切り出して認識する
    /// </summary>
    /// <param name="input">全体画像とOCR対象範囲</param>
    /// <param name="recognizeAsync">切り出した画像を認識する処理</param>
    /// <returns>OCR対象範囲と同じ順序の認識結果</returns>
    public static async ValueTask<IReadOnlyList<IReadOnlyList<TextRect>>> RecognizeRegionsAsync(
        Modules.OcrCaptureInput input,
        Func<Windows.Graphics.Imaging.SoftwareBitmap, ValueTask<IReadOnlyList<TextRect>>> recognizeAsync)
    {
        var results = new List<IReadOnlyList<TextRect>>(input.Regions.Count);

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
                results.Add(await recognizeAsync(bitmap).ConfigureAwait(false));
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
#endif

    [GeneratedRegex(@"^[\s\p{S}\p{P}\d]+$")]
    public static partial Regex AllSymbolOrSpace();
}
