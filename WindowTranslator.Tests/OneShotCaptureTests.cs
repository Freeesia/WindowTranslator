using System.Drawing;
using Microsoft.Extensions.Logging.Abstractions;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

public class OneShotCaptureTests
{
    private static readonly Size imageSize = new(320, 240);
    private static readonly TextRect previous = new("Previous text", 25, 30, 120, 25, 20, false);

    [Fact]
    public void ワンショットでは前回の追跡結果ではなく今回の観測を使う()
    {
        var tracker = new FixedTracker { Result = previous };
        var current = previous with { SourceText = "Current text", X = 27, Y = 32, FontSize = 23 };

        Assert.Equal(previous, Assert.Single(MainViewModelBase.SelectOcrTexts([previous], imageSize, tracker, true)));
        Assert.Equal(current, Assert.Single(MainViewModelBase.SelectOcrTexts([current], imageSize, tracker, true)));
        Assert.Empty(MainViewModelBase.SelectOcrTexts([], imageSize, tracker, true));
        Assert.Equal(0, tracker.UpdateCount);
    }

    [Fact]
    public void 通常モードでは追跡結果を使う()
    {
        var tracker = new FixedTracker { Result = previous };
        TextRect[] observations = [previous with { SourceText = "Current text", X = 27 }];

        Assert.Equal(previous, Assert.Single(MainViewModelBase.SelectOcrTexts(observations, imageSize, tracker, false)));
        Assert.Equal(1, tracker.UpdateCount);
        Assert.Same(observations, tracker.Observations);
        Assert.Equal(imageSize, tracker.ImageSize);
    }

    [Fact]
    public void 前回の確定値が残る状況でもワンショットは最新の観測を使う()
    {
        var tracker = new OcrTextTracker(NullLogger<OcrTextTracker>.Instance);
        var first = previous with { SourceText = "Open Options" };
        var current = first with { SourceText = "Open Option", X = 27, FontSize = 23 };

        tracker.Update([first], imageSize, TimeSpan.Zero);
        var tracked = Assert.Single(tracker.Update([current], imageSize, TimeSpan.FromMilliseconds(100)));
        Assert.Equal(first.SourceText, tracked.SourceText);
        Assert.Equal(current, Assert.Single(MainViewModelBase.SelectOcrTexts([current], imageSize, tracker, true)));
    }

    private sealed class FixedTracker : IOcrTextTracker
    {
        public TextRect Result { get; init; } = TextRect.Empty;
        public int UpdateCount { get; private set; }
        public IEnumerable<TextRect>? Observations { get; private set; }
        public Size ImageSize { get; private set; }

        public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize, TimeSpan? timestamp = null)
        {
            this.UpdateCount++;
            this.Observations = observations;
            this.ImageSize = imageSize;
            return [this.Result];
        }

        public void Reset() { }
    }
}
