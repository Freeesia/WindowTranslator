using System.Drawing;
using Microsoft.Extensions.Logging.Abstractions;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

public sealed class OcrTypewriterTrackingTests
{
    private static readonly Size imageSize = new(1280, 720);

    [Fact]
    public void ProgressiveTextStaysBusyAndOnlyTheFinalTextBecomesTranslatable()
    {
        OcrTextTracker tracker = CreateTracker();
        List<string> translationRequests = [];

        ObserveAndCollect(tracker, translationRequests, 0, Rect("H"));
        TextRect he = ObserveAndCollect(tracker, translationRequests, 500, Rect("He")).Single();
        TextRect hel = ObserveAndCollect(tracker, translationRequests, 1000, Rect("Hel")).Single();
        TextRect helloBusy = ObserveAndCollect(tracker, translationRequests, 1500, Rect("Hello")).Single();
        TextRect hello = ObserveAndCollect(tracker, translationRequests, 2000, Rect("Hello")).Single();

        Assert.Equal(TextRegionBusyReason.Typewriter, he.BusyReasons);
        Assert.Equal(TextRegionBusyReason.Typewriter, hel.BusyReasons);
        Assert.Equal(TextRegionBusyReason.Typewriter, helloBusy.BusyReasons);
        Assert.False(hello.IsBusy);
        Assert.Equal("Hello", hello.SourceText);
        Assert.Equal(["H", "Hello"], translationRequests);
    }

    [Fact]
    public void OscillatingTailSelectsTheMostProgressedCandidateAndLeavesTypewriterBusy()
    {
        OcrTextTracker tracker = CreateTracker();
        List<string> translationRequests = [];

        ObserveAndCollect(tracker, translationRequests, 0, Rect("H"));
        ObserveAndCollect(tracker, translationRequests, 500, Rect("He"));
        ObserveAndCollect(tracker, translationRequests, 1000, Rect("Hel"));
        ObserveAndCollect(tracker, translationRequests, 1500, Rect("Hello"));
        ObserveAndCollect(tracker, translationRequests, 2000, Rect("Hell0"));
        ObserveAndCollect(tracker, translationRequests, 2500, Rect("Hello"));
        TextRect final = ObserveAndCollect(tracker, translationRequests, 3000, Rect("Hell0")).Single();

        Assert.False(final.IsBusy);
        Assert.Equal("Hello", final.SourceText);
        Assert.Equal(["H", "Hello"], translationRequests);
    }

    [Fact]
    public void ReturningToTheConfirmedTextCancelsTypewriterBusy()
    {
        OcrTextTracker tracker = CreateTracker();

        TextRect original = Update(tracker, 0, Rect("Menu")).Single();
        TextRect progressing = Update(tracker, 500, Rect("Menu...")).Single();
        TextRect restored = Update(tracker, 1000, Rect("Menu")).Single();

        Assert.False(original.IsBusy);
        Assert.Equal(TextRegionBusyReason.Typewriter, progressing.BusyReasons);
        Assert.False(restored.IsBusy);
        Assert.Equal("Menu", restored.SourceText);
    }

    [Fact]
    public void OrdinaryReplacementUsesTheExistingTextConfirmation()
    {
        OcrTextTracker tracker = CreateTracker();

        Update(tracker, 0, Rect("Menu"));
        TextRect candidate = Update(tracker, 500, Rect("Game")).Single();
        TextRect confirmed = Update(tracker, 1000, Rect("Game")).Single();

        Assert.False(candidate.IsBusy);
        Assert.Equal("Menu", candidate.SourceText);
        Assert.False(confirmed.IsBusy);
        Assert.Equal("Game", confirmed.SourceText);
    }

    [Fact]
    public void ProgressionCanStartAfterThePreviousTextWasReplaced()
    {
        OcrTextTracker tracker = CreateTracker();

        Update(tracker, 0, Rect("Menu"));
        TextRect firstCharacter = Update(tracker, 500, Rect("H")).Single();
        TextRect progressing = Update(tracker, 1000, Rect("He")).Single();

        Assert.False(firstCharacter.IsBusy);
        Assert.Equal("Menu", firstCharacter.SourceText);
        Assert.Equal(TextRegionBusyReason.Typewriter, progressing.BusyReasons);
    }

    [Fact]
    public void ASingleExtensionAfterCompletionUsesTheExistingStabilization()
    {
        OcrTextTracker tracker = CreateTracker();

        Update(tracker, 0, Rect("H"));
        Update(tracker, 500, Rect("He"));
        Update(tracker, 1000, Rect("Hello"));
        TextRect completed = Update(tracker, 1500, Rect("Hello")).Single();
        TextRect noise = Update(tracker, 2000, Rect("Hello!")).Single();

        Assert.False(completed.IsBusy);
        Assert.Equal("Hello", completed.SourceText);
        Assert.False(noise.IsBusy);
        Assert.Equal("Hello", noise.SourceText);
    }

    [Fact]
    public void RapidRepeatedFramesDoNotEndTypewriterBeforeThePauseThreshold()
    {
        OcrTextTracker tracker = CreateTracker();

        Update(tracker, 0, Rect("H"));
        Update(tracker, 50, Rect("He"));
        TextRect earlyRepeat = Update(tracker, 100, Rect("He")).Single();
        TextRect stillProgressing = Update(tracker, 400, Rect("He")).Single();
        TextRect completed = Update(tracker, 550, Rect("He")).Single();

        Assert.True(earlyRepeat.IsBusy);
        Assert.True(stillProgressing.IsBusy);
        Assert.False(completed.IsBusy);
        Assert.Equal("He", completed.SourceText);
    }

    [Fact]
    public void ATypewriterTrackDoesNotBlockAnotherLogicalTrack()
    {
        OcrTextTracker tracker = CreateTracker();

        Update(tracker, 0, Rect("H"), Rect("Status", x: 400));
        IReadOnlyList<TextRect> output = Update(
            tracker,
            500,
            Rect("He"),
            Rect("Status", x: 400));

        TextRect typewriter = Assert.Single(output, text => text.X == 100);
        TextRect stable = Assert.Single(output, text => text.X == 400);
        Assert.True(typewriter.IsBusy);
        Assert.False(stable.IsBusy);
        Assert.Equal("Status", stable.SourceText);
    }

    private static OcrTextTracker CreateTracker()
        => new(NullLogger<OcrTextTracker>.Instance);

    private static IReadOnlyList<TextRect> ObserveAndCollect(
        OcrTextTracker tracker,
        List<string> translationRequests,
        int milliseconds,
        params TextRect[] observations)
    {
        IReadOnlyList<TextRect> output = Update(tracker, milliseconds, observations);
        translationRequests.AddRange(output
            .Where(text => !text.IsBusy)
            .Select(text => text.SourceText)
            .Where(text => !translationRequests.Contains(text, StringComparer.Ordinal)));
        return output;
    }

    private static IReadOnlyList<TextRect> Update(
        OcrTextTracker tracker,
        int milliseconds,
        params TextRect[] observations)
        => tracker.Update(observations, imageSize, TimeSpan.FromMilliseconds(milliseconds));

    private static TextRect Rect(string text, double x = 100)
        => new(text, x, 100, 160, 30, 20, false);
}
