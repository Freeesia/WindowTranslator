using System.Drawing;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

public class OcrObservationSelectorTests
{
    [Fact]
    public void OneShotReturnsCurrentObservationsWithoutCallingTracker()
    {
        TextRect[] observations = [new("current", 10, 20, 100, 30, 18, false)];
        var tracker = new RecordingTracker
        {
            Result = [new("previous", 1, 2, 3, 4, 5, false)],
        };

        IReadOnlyList<TextRect> result = OcrObservationSelector.Select(
            observations,
            new Size(1920, 1080),
            tracker,
            true);

        Assert.False(tracker.WasCalled);
        Assert.Equal(observations, result);
    }

    [Fact]
    public void ContinuousModeReturnsTrackerResult()
    {
        TextRect[] tracked = [new("tracked", 11, 22, 101, 31, 19, false)];
        var tracker = new RecordingTracker { Result = tracked };

        IReadOnlyList<TextRect> result = OcrObservationSelector.Select(
            [new("current", 10, 20, 100, 30, 18, false)],
            new Size(1920, 1080),
            tracker,
            false);

        Assert.True(tracker.WasCalled);
        Assert.Same(tracked, result);
    }

    private sealed class RecordingTracker : IOcrTextTracker
    {
        public bool WasCalled { get; private set; }
        public required IReadOnlyList<TextRect> Result { get; init; }

        public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize)
        {
            this.WasCalled = true;
            return this.Result;
        }

        public void Reset()
        {
        }
    }
}
