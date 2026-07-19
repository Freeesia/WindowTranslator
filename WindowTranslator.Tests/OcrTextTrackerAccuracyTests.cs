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
    public void TrackingImprovesAccuracyAcrossSeededRandomizedTraces()
    {
        List<double> legacyAccuracies = [];
        List<double> trackingAccuracies = [];
        foreach (int seed in RandomizedOcrTrackingAccuracyScenarios.Seeds)
        {
            RandomizedScenario scenario = RandomizedOcrTrackingAccuracyScenarios.Create(seed);
            LegacyOcrBufferModel legacy = new();
            OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
            List<IReadOnlyList<TextRect>> legacyFrames = [];
            List<IReadOnlyList<TextRect>> trackedFrames = [];
            for (int frame = 0; frame < scenario.Observations.Count; frame++)
            {
                IReadOnlyList<TextRect> observations = scenario.Observations[frame];
                legacyFrames.Add(legacy.Update(observations, scenario.ImageSize));
                trackedFrames.Add(tracker.Update(
                    observations,
                    scenario.ImageSize,
                    TimeSpan.FromMilliseconds(frame * 350)));
            }

            double legacyAccuracy = RandomizedOcrTrackingAccuracyScenarios.Measure(
                scenario.Expected,
                legacyFrames);
            double trackingAccuracy = RandomizedOcrTrackingAccuracyScenarios.Measure(
                scenario.Expected,
                trackedFrames);
            legacyAccuracies.Add(legacyAccuracy);
            trackingAccuracies.Add(trackingAccuracy);
            output.WriteLine(
                $"Seed {seed}: OcrBufferFilter {legacyAccuracy:P2}, OcrTextTracker {trackingAccuracy:P2}");
            Assert.True(trackingAccuracy - legacyAccuracy >= 0.08,
                $"Seed {seed} improved by only {(trackingAccuracy - legacyAccuracy):P2}.");
        }

        double averageLegacyAccuracy = legacyAccuracies.Average();
        double averageTrackingAccuracy = trackingAccuracies.Average();
        output.WriteLine(
            $"Randomized average: OcrBufferFilter {averageLegacyAccuracy:P2}, "
                + $"OcrTextTracker {averageTrackingAccuracy:P2}");

        Assert.True(averageTrackingAccuracy > averageLegacyAccuracy,
            $"Tracking accuracy {averageTrackingAccuracy:P2} did not improve on {averageLegacyAccuracy:P2}.");
        Assert.True(averageTrackingAccuracy - averageLegacyAccuracy >= 0.10,
            $"Randomized accuracy improved by only {(averageTrackingAccuracy - averageLegacyAccuracy):P2}.");
        Assert.InRange(averageTrackingAccuracy, 0.70, 0.995);
    }

    [Fact]
    public void TextAndWidthChangeDoesNotCreateOverlappingTracks()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect initial = new("AAAA", 100, 100, 100, 20, 16, false);
        TextRect changed = new("ZZZZZZZZ", 100, 100, 125, 20, 16, false);
        tracker.Update([initial], imageSize, TimeSpan.Zero);

        TextRect pending = Assert.Single(tracker.Update(
            [changed], imageSize, TimeSpan.FromMilliseconds(500)));
        TextRect confirmed = Assert.Single(tracker.Update(
            [changed], imageSize, TimeSpan.FromMilliseconds(1000)));

        Assert.Equal("AAAA", pending.SourceText);
        Assert.Equal("ZZZZZZZZ", confirmed.SourceText);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void SplitUpToTheCandidateLimitRemainsOneLogicalTrack(int partCount)
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        string text = new(Enumerable.Range(0, partCount)
            .Select(index => (char)('A' + index))
            .ToArray());
        tracker.Update([new(text, 0, 100, partCount * 20, 20, 16, false)], imageSize, TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
            Enumerable.Range(0, partCount)
                .Select(index => new TextRect(
                    ((char)('A' + index)).ToString(),
                    index * 20,
                    100,
                    20,
                    20,
                    16,
                    false)),
            imageSize,
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(text, Assert.Single(result).SourceText);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void MergeUpToTheCandidateLimitConvergesWithoutDuplicate(int trackCount)
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect[] fragments = Enumerable.Range(0, trackCount)
            .Select(index => new TextRect(
                ((char)('A' + index)).ToString(),
                index * 20,
                100,
                20,
                20,
                16,
                false))
            .ToArray();
        string text = string.Concat(fragments.Select(fragment => fragment.SourceText));
        TextRect merged = new(text, 0, 100, trackCount * 20, 20, 16, false);
        tracker.Update(fragments, imageSize, TimeSpan.Zero);

        Assert.Equal(trackCount, tracker.Update(
            [merged], imageSize, TimeSpan.FromMilliseconds(500)).Count);
        IReadOnlyList<TextRect> result = tracker.Update(
            [merged], imageSize, TimeSpan.FromMilliseconds(1000));

        Assert.Equal(text, Assert.Single(result).SourceText);
    }

    [Fact]
    public void PersistentMicroGeometryChangeEventuallyConverges()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect initial = new("Panel", 100, 100, 100, 30, 24, false) { Angle = 359 };
        TextRect changed = initial with { X = 101, Angle = 1 };
        tracker.Update([initial], imageSize, TimeSpan.Zero);

        TextRect result = initial;
        for (int frame = 1; frame <= 6; frame++)
        {
            result = Assert.Single(tracker.Update(
                [changed], imageSize, TimeSpan.FromMilliseconds(frame * 500)));
        }

        Assert.Equal(101, result.X);
        Assert.Equal(1, result.Angle);
    }

    [Fact]
    public void NonFiniteGeometryIsRejected()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect valid = new("Invalid", 10, 10, 100, 30, 24, false);
        TextRect[] invalid =
        [
            valid with { X = double.NaN },
            valid with { Y = double.PositiveInfinity },
            valid with { Width = double.PositiveInfinity },
            valid with { Height = double.PositiveInfinity },
            valid with { FontSize = double.NaN },
            valid with { Angle = double.NegativeInfinity },
        ];

        Assert.Empty(tracker.Update(invalid, imageSize, TimeSpan.Zero));
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
    public void SpatiallyDistantLongTextsAreRejectedWithoutDominatingTheFrame()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(3840, 2160);
        string initialPrefix = new('A', 2000);
        string distantPrefix = new('B', 2000);
        TextRect[] initial = Enumerable.Range(0, 64)
            .Select(index => new TextRect(
                $"{initialPrefix}-{index}",
                index % 8 * 20,
                index / 8 * 20,
                120,
                30,
                24,
                false))
            .ToArray();
        TextRect[] distant = Enumerable.Range(0, 64)
            .Select(index => new TextRect(
                $"{distantPrefix}-{index}",
                3000 + (index % 8 * 20),
                1500 + (index / 8 * 20),
                120,
                30,
                24,
                false))
            .ToArray();
        tracker.Update(initial, imageSize);

        Stopwatch stopwatch = Stopwatch.StartNew();
        tracker.Update(distant, imageSize);
        stopwatch.Stop();

        output.WriteLine($"64x64 spatially distant long-text candidates: {stopwatch.ElapsedMilliseconds} ms");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500), $"Spatial rejection took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void DenseOneToOneAssignmentPreservesMaximumCardinality()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect[] tracks = Enumerable.Range(0, 9)
            .Select(index => new TextRect(
                "A",
                index < 3 ? 160 + index : 100 + index - 3,
                100,
                100,
                30,
                24,
                false))
            .ToArray();
        TextRect[] observations = Enumerable.Range(0, 9)
            .Select(index => new TextRect(
                "A",
                index < 3 ? 100 + index : 160 + index - 3,
                100,
                100,
                30,
                24,
                false))
            .ToArray();
        tracker.Update(tracks, imageSize, TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
            observations,
            imageSize,
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(9, result.Count);
    }

    [Fact]
    public void DenseOneToOneAssignmentPreservesMaximumCardinalityWithTenTracks()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect[] tracks = Enumerable.Range(0, 10)
            .Select(index => new TextRect(
                "A",
                index < 3 ? 160 + index : 100 + index - 3,
                100,
                100,
                30,
                24,
                false))
            .ToArray();
        TextRect[] observations = Enumerable.Range(0, 10)
            .Select(index => new TextRect(
                "A",
                index < 3 ? 100 + index : 160 + index - 3,
                100,
                100,
                30,
                24,
                false))
            .ToArray();
        tracker.Update(tracks, imageSize, TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
            observations,
            imageSize,
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void MaximumCardinalityAssignmentStillMaximizesTheTotalScore()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect left = new TextRect("A", 0, 100, 100, 30, 24, false) { Context = "Left" };
        TextRect right = new TextRect("A", 30, 100, 100, 30, 24, false) { Context = "Right" };
        tracker.Update([left, right], imageSize, TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
        [
            left with { X = 10, Context = "NearLeft" },
            right with { X = 50, Context = "NearRight" },
        ],
        imageSize,
        TimeSpan.FromMilliseconds(500));

        Assert.Equal(["NearLeft", "NearRight"], result.Select(rect => rect.Context));
    }

    [Fact]
    public void WeakAssignmentDoesNotScaleWithReservedObservations()
    {
        Size imageSize = new(1920, 1080);
        TextRect[] observations = Enumerable.Range(0, 200)
            .Select(index => new TextRect(
                new string((char)(0x1000 + index), 8),
                (index % 20) * 90,
                (index / 20) * 90,
                60,
                30,
                24,
                false))
            .ToArray();
        TextRect[] oneWeakObservation = observations.ToArray();
        oneWeakObservation[^1] = oneWeakObservation[^1] with
        {
            SourceText = oneWeakObservation[^1].SourceText + "X",
        };

        MeasureAllocation(observations);
        MeasureAllocation(oneWeakObservation);
        long strongAllocation = MeasureAllocation(observations);
        long weakAllocation = MeasureAllocation(oneWeakObservation);
        long weakAssignmentAllocation = weakAllocation - strongAllocation;

        output.WriteLine($"Additional allocation for one weak assignment: {weakAssignmentAllocation:N0} bytes");
        Assert.True(weakAssignmentAllocation < 256 * 1024,
            $"One weak assignment allocated an additional {weakAssignmentAllocation:N0} bytes.");

        long MeasureAllocation(TextRect[] current)
        {
            OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
            tracker.Update(observations, imageSize, TimeSpan.Zero);
            long before = GC.GetAllocatedBytesForCurrentThread();
            tracker.Update(current, imageSize, TimeSpan.FromMilliseconds(500));
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }

    [Fact]
    public void IndependentOneToOneAssignmentsDoNotAllocateAsADenseMatrix()
    {
        MeasureAllocation(80);
        MeasureAllocation(320);
        long smallAllocation = MeasureAllocation(80);
        long largeAllocation = MeasureAllocation(320);

        output.WriteLine($"80 independent assignments: {smallAllocation:N0} bytes");
        output.WriteLine($"320 independent assignments: {largeAllocation:N0} bytes");
        Assert.True(largeAllocation < smallAllocation * 8,
            $"Quadrupling independent assignments increased allocation from "
                + $"{smallAllocation:N0} to {largeAllocation:N0} bytes.");

        static long MeasureAllocation(int count)
        {
            OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
            Size imageSize = new(1000, 600);
            TextRect[] observations = Enumerable.Range(0, count)
                .Select(index => new TextRect(
                    new string((char)(0x1000 + index), 8),
                    index * 200,
                    100,
                    30,
                    20,
                    16,
                    false))
                .ToArray();
            tracker.Update(observations, imageSize, TimeSpan.Zero);
            long before = GC.GetAllocatedBytesForCurrentThread();
            tracker.Update(observations, imageSize, TimeSpan.FromMilliseconds(500));
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }

    [Fact]
    public void MixedStructureSelectionDoesNotDominateAFrame()
    {
        Size imageSize = new(1000, 600);
        TextRect[] tracks = Enumerable.Range(0, 9)
            .Select(index => new TextRect("AA", 100 + index, 100, 100, 30, 24, false))
            .ToArray();
        TextRect[] observations = Enumerable.Range(0, 9)
            .Select(index => new TextRect("A", 100 + index, 100, 100, 30, 24, false))
            .ToArray();
        RunFrame();
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int iteration = 0; iteration < 10; iteration++)
        {
            Assert.Equal(9, RunFrame().Count);
        }
        stopwatch.Stop();

        output.WriteLine($"10 frames of 9x9 mixed structure candidates: {stopwatch.ElapsedMilliseconds} ms");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"10 mixed structure frames took {stopwatch.Elapsed}.");

        IReadOnlyList<TextRect> RunFrame()
        {
            OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
            tracker.Update(tracks, imageSize, TimeSpan.Zero);
            return tracker.Update(observations, imageSize, TimeSpan.FromMilliseconds(500));
        }
    }

    [Fact]
    public void DenseRejectedLongStructureCandidatesDoNotDominateAFrame()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        string previousText = new('B', 1000);
        string currentText = new('A', 1000);
        TextRect[] tracks = Enumerable.Range(0, 6)
            .Select(index => new TextRect(previousText, 100 + index, 100, 100, 30, 24, false))
            .ToArray();
        TextRect[] observations = Enumerable.Range(0, 6)
            .Select(index => new TextRect(currentText, 100 + index, 100, 100, 30, 24, false))
            .ToArray();
        tracker.Update(tracks, imageSize, TimeSpan.Zero);

        Stopwatch stopwatch = Stopwatch.StartNew();
        tracker.Update(observations, imageSize, TimeSpan.FromMilliseconds(500));
        stopwatch.Stop();

        output.WriteLine($"Dense rejected long structure candidates: {stopwatch.ElapsedMilliseconds} ms");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Dense long-text rejection took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void StructureSelectionPrefersGreaterCoverageOverHigherIndividualScores()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect[] tracks =
        [
            new("A", 0, 100, 10, 20, 16, false),
            new("B", 10, 100, 10, 20, 16, false),
            new("C", 20, 100, 10, 20, 16, false),
            new("D", 30, 100, 10, 20, 16, false),
            new("E", 40, 100, 10, 20, 16, false),
            new("F", 50, 100, 10, 20, 16, false),
        ];
        tracker.Update(tracks, imageSize, TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
        [
            new("ABC", 0, 100, 30, 20, 16, false),
            new("DEF", 30, 100, 30, 20, 16, false),
            new("AD", 0, 100, 44, 20, 16, false),
            new("BE", 10, 100, 44, 20, 16, false),
            new("CF", 20, 100, 44, 20, 16, false),
        ],
            imageSize,
            TimeSpan.FromMilliseconds(500));

        Assert.Equal(
            ["A", "B", "C", "D", "E", "F", "ABC", "DEF"],
            result.Select(rect => rect.SourceText));
    }

    [Fact]
    public void StructureSelectionEscapesLargeAndCompactGreedyTraps()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        tracker.Update(
        [
            new("A", -20, 100, 20, 20, 16, false),
            new("BCDE", 0, 100, 100, 20, 16, false),
            new("B", 0, 100, 25, 20, 16, false),
            new("C", 25, 100, 25, 20, 16, false),
            new("D", 50, 100, 25, 20, 16, false),
            new("E", 75, 100, 25, 20, 16, false),
        ],
        imageSize,
        TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
        [
            new("ABCDE", -20, 100, 124, 20, 16, false),
            new("BC", 0, 100, 50, 20, 16, false),
            new("DE", 50, 100, 50, 20, 16, false),
            new("ABD", -20, 100, 95, 20, 16, false),
            new("CBCDEE", 0, 100, 100, 20, 16, false),
        ],
        imageSize,
        TimeSpan.FromMilliseconds(500));

        Assert.Equal(
            ["A", "BCDE", "B", "C", "D", "E", "ABD", "CBCDEE"],
            result.Select(rect => rect.SourceText));
    }

    [Fact]
    public void StructureSelectionReplacesOneBlockingCandidateWithTwoMatches()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        tracker.Update(
        [
            new("A", 0, 100, 10, 20, 16, false),
            new("B", 10, 100, 10, 20, 16, false),
            new("C", 20, 100, 10, 20, 16, false),
            new("D", 30, 100, 10, 20, 16, false),
        ],
        imageSize,
        TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
        [
            new("AB", 0, 100, 24, 20, 16, false),
            new("CD", 20, 100, 24, 20, 16, false),
            new("ABC", 0, 100, 30, 20, 16, false),
        ],
        imageSize,
        TimeSpan.FromMilliseconds(500));

        Assert.Equal(
            ["A", "B", "C", "D", "ABC"],
            result.Select(rect => rect.SourceText));
    }

    [Fact]
    public void StructureSelectionCanReplaceCoupledBlockingCandidates()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        string longTail = new('A', 10);
        tracker.Update(
        [
            new("XYZ", 0, 100, 30, 20, 16, false),
            new("Q", 300, 100, 10, 20, 16, false),
            new("B", 30, 100, 10, 20, 16, false),
            new("C", 40, 100, 10, 20, 16, false),
            new("D", 50, 100, 10, 20, 16, false),
            new(longTail, 60, 100, 100, 20, 16, false),
        ],
        imageSize,
        TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
        [
            new("Novel", 500, 100, 50, 20, 16, false),
            new("X", 0, 100, 10, 20, 16, false),
            new("Y", 10, 100, 10, 20, 16, false),
            new("XYZBC", 0, 100, 50, 20, 16, false),
            new($"BCD{longTail}", 30, 100, 130, 20, 16, false),
            new("Z", 20, 100, 10, 20, 16, false),
        ],
        imageSize,
        TimeSpan.FromMilliseconds(500));

        Assert.Equal(
            ["XYZ", "Q", "B", "C", "D", longTail, "Novel", "XYZBC"],
            result.Select(rect => rect.SourceText));
    }

    [Fact]
    public void FourPartSplitRemainsOneLogicalTrack()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        tracker.Update([new("ABCD", 0, 100, 80, 20, 16, false)], imageSize, TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
        [
            new("A", 0, 100, 20, 20, 16, false),
            new("B", 20, 100, 20, 20, 16, false),
            new("C", 40, 100, 20, 20, 16, false),
            new("D", 60, 100, 20, 20, 16, false),
        ],
        imageSize,
        TimeSpan.FromMilliseconds(500));

        Assert.Equal("ABCD", Assert.Single(result).SourceText);
    }

    [Fact]
    public void FourTrackMergeConvergesWithoutDuplicate()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect[] fragments =
        [
            new("A", 0, 100, 20, 20, 16, false),
            new("B", 20, 100, 20, 20, 16, false),
            new("C", 40, 100, 20, 20, 16, false),
            new("D", 60, 100, 20, 20, 16, false),
        ];
        TextRect merged = new("ABCD", 0, 100, 80, 20, 16, false);
        tracker.Update(fragments, imageSize, TimeSpan.Zero);

        Assert.Equal(4, tracker.Update([merged], imageSize, TimeSpan.FromMilliseconds(500)).Count);
        IReadOnlyList<TextRect> result = tracker.Update(
            [merged],
            imageSize,
            TimeSpan.FromMilliseconds(1000));

        Assert.Equal("ABCD", Assert.Single(result).SourceText);
    }

    [Fact]
    public void EquivalentStructureCandidatesDoNotDependOnObservationOrder()
    {
        Size imageSize = new(1000, 600);
        TextRect[] observations =
        [
            new TextRect("A", 40, 100, 18, 20, 16, false) { Context = "Left" },
            new("B", 62, 100, 18, 20, 16, false),
            new TextRect("A", 120, 100, 18, 20, 16, false) { Context = "Right" },
            new("B", 142, 100, 18, 20, 16, false),
        ];

        string? forward = SelectContext(observations);
        string? reversed = SelectContext(observations.Reverse().ToArray());

        Assert.Equal("Left", forward);
        Assert.Equal(forward, reversed);

        string? SelectContext(TextRect[] current)
        {
            OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
            tracker.Update([new("AB", 80, 100, 40, 20, 16, false)], imageSize, TimeSpan.Zero);
            IReadOnlyList<TextRect> result = tracker.Update(
                current,
                imageSize,
                TimeSpan.FromMilliseconds(500));
            return Assert.Single(result, rect => rect.SourceText == "AB").Context;
        }
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
    public void PersistentSmallGeometryChangeEventuallyConverges()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect initial = new("Panel", 100, 100, 100, 30, 24, false);
        TextRect changed = initial with { X = 110, Width = 110 };
        tracker.Update([initial], imageSize, TimeSpan.Zero);

        TextRect first = Assert.Single(tracker.Update(
            [changed],
            imageSize,
            TimeSpan.FromMilliseconds(500)));
        TextRect confirmed = Assert.Single(tracker.Update(
            [changed],
            imageSize,
            TimeSpan.FromMilliseconds(1000)));

        Assert.Equal((100, 100), (first.X, first.Width));
        Assert.Equal((110, 110), (confirmed.X, confirmed.Width));
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
    public void DominantFragmentDoesNotSuppressAnExactSplitCandidate()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        tracker.Update([new("ABCDEFG", 0, 100, 200, 30, 24, false)], imageSize, TimeSpan.Zero);

        TextRect result = Assert.Single(tracker.Update(
        [
            new("ABCDEF", 0, 100, 180, 30, 24, false),
            new("G", 185, 100, 15, 30, 24, false),
        ],
        imageSize,
        TimeSpan.FromMilliseconds(500)));

        Assert.Equal("ABCDEFG", result.SourceText);
    }

    [Fact]
    public void StrongOneToOneMatchIsReservedBeforeMergeCandidates()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect first = new("A", 0, 100, 40, 30, 24, false);
        TextRect second = new("B", 50, 100, 40, 30, 24, false);
        tracker.Update([first, second], imageSize, TimeSpan.Zero);

        IReadOnlyList<TextRect> result = tracker.Update(
        [
            first,
            new("AB", 0, 100, 90, 30, 24, false),
        ],
        imageSize,
        TimeSpan.FromMilliseconds(500));

        Assert.Equal(["A", "B", "AB"], result.Select(rect => rect.SourceText));
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
    public void DormantChildrenReturnAfterTheMergedParentMoves()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect first = new("New", 0, 100, 40, 30, 24, false);
        TextRect second = new("Game", 50, 100, 50, 30, 24, false);
        TextRect merged = new("New Game", 0, 100, 100, 30, 24, false);
        TextRect movedMerged = merged with { X = 200 };
        TextRect movedFirst = first with { X = 200 };
        TextRect movedSecond = second with { X = 250 };

        tracker.Update([first, second], imageSize, TimeSpan.Zero);
        tracker.Update([merged], imageSize, TimeSpan.FromMilliseconds(500));
        Assert.Single(tracker.Update([merged], imageSize, TimeSpan.FromMilliseconds(1000)));
        Assert.Single(tracker.Update([movedMerged], imageSize, TimeSpan.FromMilliseconds(1500)));
        Assert.Single(tracker.Update([movedFirst, movedSecond], imageSize, TimeSpan.FromMilliseconds(2000)));

        IReadOnlyList<TextRect> restored = tracker.Update(
            [movedFirst, movedSecond], imageSize, TimeSpan.FromMilliseconds(2500));
        Assert.Equal(["New", "Game"], restored.Select(rect => rect.SourceText));
    }

    [Fact]
    public void DormantChildrenReturnWhenMovementAndSplitOccurTogether()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect first = new("New", 0, 100, 40, 30, 24, false);
        TextRect second = new("Game", 50, 100, 50, 30, 24, false);
        TextRect merged = new("New Game", 0, 100, 100, 30, 24, false);
        TextRect movedFirst = first with { X = 200 };
        TextRect movedSecond = second with { X = 250 };

        tracker.Update([first, second], imageSize, TimeSpan.Zero);
        tracker.Update([merged], imageSize, TimeSpan.FromMilliseconds(500));
        Assert.Single(tracker.Update([merged], imageSize, TimeSpan.FromMilliseconds(1000)));

        Assert.Single(tracker.Update(
            [movedFirst, movedSecond], imageSize, TimeSpan.FromMilliseconds(1500)));
        IReadOnlyList<TextRect> restored = tracker.Update(
            [movedFirst, movedSecond], imageSize, TimeSpan.FromMilliseconds(2000));
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

    [Theory]
    [InlineData(60, 24, 0, 60, 24, 0)]
    [InlineData(30, 48, 0, 30, 36, 0)]
    [InlineData(30, 24, 20, 30, 24, 10)]
    public void SplitAndMergeCandidatesRequireCompatibleMemberStyles(
        double secondHeight,
        double secondFontSize,
        double secondAngle,
        double combinedHeight,
        double combinedFontSize,
        double combinedAngle)
    {
        Size imageSize = new(1000, 600);
        TextRect first = new("New", 0, 100, 95, 30, 24, false) { Angle = 0 };
        TextRect second = new("Game", 100, 100, 100, secondHeight, secondFontSize, false) { Angle = secondAngle };
        TextRect combined = new("New Game", 0, 100, 200, combinedHeight, combinedFontSize, false) { Angle = combinedAngle };

        OcrTextTracker splitTracker = new(NullLogger<OcrTextTracker>.Instance);
        splitTracker.Update([combined], imageSize, TimeSpan.Zero);
        Assert.True(
            splitTracker.Update([first, second], imageSize, TimeSpan.FromMilliseconds(500)).Count > 1,
            "Incompatible OCR fragments must not be collapsed into one logical track.");

        OcrTextTracker mergeTracker = new(NullLogger<OcrTextTracker>.Instance);
        mergeTracker.Update([first, second], imageSize, TimeSpan.Zero);
        mergeTracker.Update([combined], imageSize, TimeSpan.FromMilliseconds(500));
        Assert.True(
            mergeTracker.Update([combined], imageSize, TimeSpan.FromMilliseconds(1000)).Count > 1,
            "Incompatible tracks must not converge into one logical track.");
    }

    [Fact]
    public void SplitUsesCircularMeanForAnglesAcrossTheWrapBoundary()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect combined = new("AB", 0, 100, 200, 30, 24, false) { Angle = 0 };
        TextRect first = new("A", 0, 100, 95, 30, 24, false) { Angle = 359 };
        TextRect second = new("B", 100, 100, 100, 30, 24, false) { Angle = 1 };
        tracker.Update([combined], imageSize, TimeSpan.Zero);

        tracker.Update([first, second], imageSize, TimeSpan.FromMilliseconds(500));
        TextRect result = Assert.Single(tracker.Update(
            [first, second], imageSize, TimeSpan.FromMilliseconds(1000)));

        Assert.InRange(Math.Abs(result.Angle), 0, 0.001);
    }

    [Fact]
    public void SmallAngleJitterAcrossTheWrapBoundaryIsSuppressed()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect initial = new("A", 0, 100, 100, 30, 24, false) { Angle = 359 };
        TextRect jittered = initial with { Angle = 1 };
        tracker.Update([initial], imageSize, TimeSpan.Zero);

        tracker.Update([jittered], imageSize, TimeSpan.FromMilliseconds(500));
        TextRect result = Assert.Single(tracker.Update(
            [jittered], imageSize, TimeSpan.FromMilliseconds(1000)));

        Assert.Equal(359, result.Angle);
    }

    [Fact]
    public void SplitOrdersFragmentsAlongTheRotatedReadingDirection()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect combined = new("AB", 105, 100, 205, 30, 24, false) { Angle = 180 };
        TextRect first = new("A", 105, 100, 95, 30, 24, false) { Angle = 180 };
        TextRect second = new("B", 0, 100, 100, 30, 24, false) { Angle = 180 };
        tracker.Update([combined], imageSize, TimeSpan.Zero);

        TextRect result = Assert.Single(tracker.Update(
            [first, second], imageSize, TimeSpan.FromMilliseconds(500)));

        Assert.Equal("AB", result.SourceText);
    }

    [Fact]
    public void PersistentMergeOrdersTracksAlongTheRotatedReadingDirection()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect first = new("A", 105, 100, 95, 30, 24, false) { Angle = 180 };
        TextRect second = new("B", 0, 100, 100, 30, 24, false) { Angle = 180 };
        TextRect combined = new("AB", 105, 100, 205, 30, 24, false) { Angle = 180 };
        tracker.Update([first, second], imageSize, TimeSpan.Zero);

        Assert.Equal(2, tracker.Update(
            [combined], imageSize, TimeSpan.FromMilliseconds(500)).Count);
        TextRect result = Assert.Single(tracker.Update(
            [combined], imageSize, TimeSpan.FromMilliseconds(1000)));

        Assert.Equal("AB", result.SourceText);
    }

    [Fact]
    public void SplitTreatsObliqueFragmentsOnTheSameReadingAxisAsAdjacent()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        double offset = 105 / Math.Sqrt(2);
        TextRect combined = new("AB", 0, 0, 205, 20, 18, false) { Angle = 45 };
        TextRect first = new("A", 0, 0, 100, 20, 18, false) { Angle = 45 };
        TextRect second = new("B", offset, offset, 100, 20, 18, false) { Angle = 45 };
        tracker.Update([combined], imageSize, TimeSpan.Zero);

        TextRect result = Assert.Single(tracker.Update(
            [first, second], imageSize, TimeSpan.FromMilliseconds(500)));

        Assert.Equal("AB", result.SourceText);
    }

    [Fact]
    public void PersistentMergeCombinesOrientedRectsAlongTheReadingAxis()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        double offset = 105 / Math.Sqrt(2);
        TextRect first = new("A", 0, 0, 100, 20, 18, false) { Angle = 45 };
        TextRect second = new("B", offset, offset, 100, 20, 18, false) { Angle = 45 };
        TextRect combined = new("AB", 0, 0, 205, 20, 18, false) { Angle = 45 };
        tracker.Update([first, second], imageSize, TimeSpan.Zero);

        Assert.Equal(2, tracker.Update(
            [combined], imageSize, TimeSpan.FromMilliseconds(500)).Count);
        TextRect result = Assert.Single(tracker.Update(
            [combined], imageSize, TimeSpan.FromMilliseconds(1000)));

        Assert.Equal("AB", result.SourceText);
    }

    [Fact]
    public void DormantChildrenDoNotConsumeAnActiveTracksObservation()
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        Size imageSize = new(1000, 600);
        TextRect childNew = RectWithContext("New", 0, "ChildNew");
        TextRect childGame = RectWithContext("Game", 50, "ChildGame");
        TextRect merged = new("New Game", 0, 100, 100, 30, 24, false) { Context = "Merged" };

        tracker.Update(
            [childNew, childGame, RectWithContext("New", 100, "Active")],
            imageSize,
            TimeSpan.Zero);
        tracker.Update(
            [merged, RectWithContext("New", 100, "Active")],
            imageSize,
            TimeSpan.FromMilliseconds(500));
        tracker.Update(
            [merged, RectWithContext("New", 100, "Active")],
            imageSize,
            TimeSpan.FromMilliseconds(1000));

        TextRect[] observations =
        [
            RectWithContext("New", 100, "Active"),
            childGame,
        ];
        tracker.Update(observations, imageSize, TimeSpan.FromMilliseconds(1500));
        IReadOnlyList<TextRect> result = tracker.Update(
            observations,
            imageSize,
            TimeSpan.FromMilliseconds(2000));

        Assert.Single(result, rect => rect.Context == "Active");
    }

    [Fact]
    public void RemovedFeaturesAreNotExposed()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;
        Type appResources = typeof(OcrTextTracker).Assembly.GetType("WindowTranslator.Properties.Resources", throwOnError: true)!;
        Type abstractionResources = typeof(TextRect).Assembly.GetType("WindowTranslator.Properties.Resources", throwOnError: true)!;

        Assert.Null(appResources.GetProperty("IsOneShotMode", flags));
        Assert.Null(abstractionResources.GetProperty("Buffer", flags));
        Assert.Null(abstractionResources.GetProperty("BufferSize", flags));
        Assert.Null(abstractionResources.GetProperty("IsSuppressVibe", flags));
        Assert.Null(abstractionResources.GetProperty("IsEnableRecover", flags));
        Assert.DoesNotContain(
            typeof(IOcrTextTracker).GetMethods(),
            method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(TimeSpan)));
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
