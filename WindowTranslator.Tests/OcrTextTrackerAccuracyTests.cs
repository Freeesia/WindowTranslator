using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Logging.Abstractions;
using Quickenshtein;
using WindowTranslator.Modules.Ocr;
using Xunit.Abstractions;

namespace WindowTranslator.Tests;

public class OcrTextTrackerAccuracyTests(ITestOutputHelper output)
{
    [Fact]
    public void TrackingImprovesAccuracyOverTheExistingBufferFilter()
    {
        IReadOnlyList<IReadOnlyList<TextRect>> legacyFrames = RunLegacyBuffer();
        IReadOnlyList<IReadOnlyList<TextRect>> trackedFrames = RunTracker();

        double legacyAccuracy = OcrTrackingAccuracyScenarios.Measure(legacyFrames);
        double trackingAccuracy = OcrTrackingAccuracyScenarios.Measure(trackedFrames);
        output.WriteLine($"OcrBufferFilter baseline accuracy: {legacyAccuracy:P2}");
        output.WriteLine($"OcrTextTracker accuracy: {trackingAccuracy:P2}");

        Assert.InRange(legacyAccuracy, 0.8005, 0.8007);
        Assert.True(trackingAccuracy >= 0.95, $"Tracking accuracy was {trackingAccuracy:P2}.");
        Assert.True(trackingAccuracy - legacyAccuracy >= 0.14,
            $"Expected at least a 14 point improvement, but measured {(trackingAccuracy - legacyAccuracy):P2}.");
    }

    [Fact]
    public void MissingObservationsExpireAfterTheRetentionWindow()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect observation = new("Temporary", 20, 20, 120, 30, 24, false);

        Assert.Single(tracker.Update([observation], imageSize));
        Assert.Single(tracker.Update([], imageSize));
        Assert.Single(tracker.Update([], imageSize));
        Assert.Single(tracker.Update([], imageSize));
        Assert.Empty(tracker.Update([], imageSize));
    }

    [Fact]
    public void PersistentMergeConvergesAfterConfirmation()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect first = new("New", 50, 400, 70, 32, 25, false);
        TextRect second = new("Game", 125, 400, 90, 32, 25, false);
        TextRect merged = new("New Game", 50, 400, 165, 32, 25, false);

        Assert.Equal(2, tracker.Update([first, second], imageSize).Count);
        Assert.Equal(2, tracker.Update([merged], imageSize).Count);
        TextRect result = Assert.Single(tracker.Update([merged], imageSize));
        Assert.Equal("New Game", result.SourceText);
    }

    [Fact]
    public void TrackingWorkDoesNotDominateAnOcrFrame()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1920, 1080);
        TextRect[] observations = Enumerable.Range(0, 30)
            .Select(index => new TextRect($"Text {index}", (index % 10) * 180, (index / 10) * 100, 120, 32, 25, false))
            .ToArray();
        tracker.Update(observations, imageSize);

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int frame = 0; frame < 100; frame++)
        {
            TextRect[] jittered = observations
                .Select(rect => rect with { X = rect.X + (frame % 3) - 1, Y = rect.Y + (frame % 2) })
                .ToArray();
            Assert.Equal(30, tracker.Update(jittered, imageSize).Count);
        }
        stopwatch.Stop();

        output.WriteLine($"100 frames with 30 tracks: {stopwatch.ElapsedMilliseconds} ms");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Tracking took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void MotionPredictionKeepsIdentityWhenTracksCrossBetweenSlowOcrFrames()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);

        tracker.Update(
        [
            RectWithContext("Item", 0, "A"),
            RectWithContext("Item", 300, "B"),
        ], imageSize, TimeSpan.Zero);
        tracker.Update(
        [
            RectWithContext("Item", 100, "A"),
            RectWithContext("Item", 200, "B"),
        ], imageSize, TimeSpan.FromMilliseconds(500));

        IReadOnlyList<TextRect> crossed = tracker.Update(
        [
            RectWithContext("Item", 220, "A"),
            RectWithContext("Item", 80, "B"),
        ], imageSize, TimeSpan.FromMilliseconds(1000));

        Assert.Equal(["A", "B"], crossed.Select(rect => rect.Context));
    }

    [Fact]
    public void GlobalSelectionPrefersExactSplitOverPartialOneToOneMatch()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        tracker.Update([new("New Game", 0, 0, 200, 30, 24, false)], imageSize, TimeSpan.Zero);

        TextRect result = Assert.Single(tracker.Update(
        [
            new("New", 0, 0, 65, 30, 24, false),
            new("Game", 70, 0, 130, 30, 24, false),
        ], imageSize, TimeSpan.FromMilliseconds(500)));

        Assert.Equal("New Game", result.SourceText);
    }

    [Fact]
    public void DormantChildrenReturnWhenAConfirmedMergeSplitsAgain()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect first = new("New", 50, 400, 70, 32, 25, false);
        TextRect second = new("Game", 125, 400, 90, 32, 25, false);
        TextRect merged = new("New Game", 50, 400, 165, 32, 25, false);

        tracker.Update([first, second], imageSize, TimeSpan.Zero);
        tracker.Update([merged], imageSize, TimeSpan.FromMilliseconds(500));
        Assert.Single(tracker.Update([merged], imageSize, TimeSpan.FromMilliseconds(1000)));
        Assert.Single(tracker.Update([first, second], imageSize, TimeSpan.FromMilliseconds(1500)));

        IReadOnlyList<TextRect> restored = tracker.Update(
            [first, second], imageSize, TimeSpan.FromMilliseconds(2000));
        Assert.Equal(["New", "Game"], restored.Select(rect => rect.SourceText));
    }

    [Fact]
    public void SimilarButDifferentOcrErrorsDoNotShareAStringVote()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);

        tracker.Update([new("Start Game", 100, 100, 200, 40, 32, false)], imageSize, TimeSpan.Zero);
        tracker.Update([new("Start Garne", 100, 100, 200, 40, 32, false)], imageSize, TimeSpan.FromMilliseconds(500));
        TextRect afterDifferentErrors = Assert.Single(tracker.Update(
            [new("Start Ganne", 100, 100, 200, 40, 32, false)], imageSize, TimeSpan.FromMilliseconds(1000)));
        Assert.Equal("Start Game", afterDifferentErrors.SourceText);

        TextRect afterRepeatedError = Assert.Single(tracker.Update(
            [new("Start Ganne", 100, 100, 200, 40, 32, false)], imageSize, TimeSpan.FromMilliseconds(1500)));
        Assert.Equal("Start Ganne", afterRepeatedError.SourceText);
    }

    private static IReadOnlyList<IReadOnlyList<TextRect>> RunLegacyBuffer()
    {
        List<IReadOnlyList<TextRect>> actual = [];
        foreach (Scenario scenario in OcrTrackingAccuracyScenarios.All)
        {
            LegacyOcrBufferModel filter = new();
            foreach (IReadOnlyList<TextRect> frame in scenario.Observations)
            {
                actual.Add(filter.Update(frame, new(1000, 600)));
            }
        }
        return actual;
    }

    private static IReadOnlyList<IReadOnlyList<TextRect>> RunTracker()
    {
        List<IReadOnlyList<TextRect>> actual = [];
        foreach (Scenario scenario in OcrTrackingAccuracyScenarios.All)
        {
            OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
            foreach (IReadOnlyList<TextRect> frame in scenario.Observations)
            {
                actual.Add(tracker.Update(frame, new(1000, 600)));
            }
        }
        return actual;
    }

    private static TextRect RectWithContext(string text, double x, string context)
        => new TextRect(text, x, 100, 40, 30, 24, false) { Context = context };
}

internal sealed class LegacyOcrBufferModel
{
    private const int BufferSize = 3;
    private readonly Queue<List<TextRect>> buffer = [];

    public IReadOnlyList<TextRect> Update(IReadOnlyList<TextRect> observations, Size imageSize)
    {
        Size threshold = new((int)(imageSize.Width * 0.02), (int)(imageSize.Height * 0.02));
        List<TextRect> bufferedTexts = [];
        foreach (TextRect rect in this.buffer.SelectMany(frame => frame))
        {
            if (!bufferedTexts.Any(existing => AreSimilar(existing, rect, threshold)))
            {
                bufferedTexts.Add(rect);
            }
        }

        if (this.buffer.Count == BufferSize)
        {
            this.buffer.Dequeue();
        }

        List<TextRect> current = [];
        List<TextRect> output = [];
        foreach (TextRect observation in observations)
        {
            TextRect text = observation;
            TextRect? past = bufferedTexts.FirstOrDefault(buffered => AreSimilar(buffered, text, threshold));
            if (past is not null)
            {
                text = text with
                {
                    X = past.X,
                    Y = past.Y,
                    Width = Math.Max(text.Width, past.Width),
                    Height = past.Height,
                    FontSize = past.FontSize,
                };
            }
            current.Add(text);
            output.Add(text);
        }
        this.buffer.Enqueue(current);

        foreach (TextRect buffered in bufferedTexts)
        {
            if (!current.Any(text => AreSimilar(text, buffered, threshold) || Intersects(text, buffered))
                && !output.Any(existing => Intersects(existing, buffered)))
            {
                output.Add(buffered);
            }
        }
        return output;
    }

    private static bool Intersects(TextRect first, TextRect second)
        => first.X < second.X + second.Width
            && first.X + first.Width > second.X
            && first.Y < second.Y + second.Height
            && first.Y + first.Height > second.Y;

    private static bool AreSimilar(TextRect first, TextRect second, Size threshold)
    {
        int maximumLength = Math.Max(first.SourceText.Length, second.SourceText.Length);
        double textSimilarity = maximumLength == 0
            ? 1
            : 1 - ((double)Levenshtein.GetDistance(first.SourceText, second.SourceText, CalculationOptions.DefaultWithThreading) / maximumLength);
        return textSimilarity >= 0.8
            && Math.Abs(first.X - second.X) <= threshold.Width
            && Math.Abs(first.Y - second.Y) <= threshold.Height
            && Math.Abs(first.Width - second.Width) <= threshold.Width
            && Math.Abs(first.Height - second.Height) <= threshold.Height;
    }
}
