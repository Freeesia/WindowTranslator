using System.Drawing;

namespace WindowTranslator.Modules.Ocr;

internal static class OcrObservationSelector
{
    public static IReadOnlyList<TextRect> Select(
        IEnumerable<TextRect> observations,
        Size imageSize,
        IOcrTextTracker tracker,
        bool isOneShotMode)
    {
        var current = observations.ToArray();
        return isOneShotMode ? current : tracker.Update(current, imageSize);
    }
}
