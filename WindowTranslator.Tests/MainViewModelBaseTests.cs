using System.Drawing;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

public class MainViewModelBaseTests
{
    [Theory]
    [InlineData(1200, 600)]
    [InlineData(1000, 700)]
    public void ImageSizeChangeResetsOcrTextTracker(int width, int height)
    {
        RecordingOcrTextTracker tracker = new();

        bool reset = MainViewModelBase.ResetOcrTextTrackerIfImageSizeChanged(
            tracker,
            new(1000, 600),
            new(width, height));

        Assert.True(reset);
        Assert.Equal(1, tracker.ResetCount);
    }

    [Fact]
    public void SameImageSizeDoesNotResetOcrTextTracker()
    {
        RecordingOcrTextTracker tracker = new();

        bool reset = MainViewModelBase.ResetOcrTextTrackerIfImageSizeChanged(
            tracker,
            new(1000, 600),
            new(1000, 600));

        Assert.False(reset);
        Assert.Equal(0, tracker.ResetCount);
    }

    [Fact]
    public void FirstImageSizeDoesNotResetOcrTextTracker()
    {
        RecordingOcrTextTracker tracker = new();

        bool reset = MainViewModelBase.ResetOcrTextTrackerIfImageSizeChanged(
            tracker,
            null,
            new(1000, 600));

        Assert.False(reset);
        Assert.Equal(0, tracker.ResetCount);
    }

    private sealed class RecordingOcrTextTracker : IOcrTextTracker
    {
        public int ResetCount { get; private set; }

        public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize)
            => observations.ToArray();

        public void Reset() => this.ResetCount++;
    }
}
