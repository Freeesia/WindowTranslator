using System.Drawing;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.Settings;

namespace WindowTranslator.Tests;

public class OcrTextTrackerSettingsTests
{
    private static readonly Size imageSize = new(1000, 600);
    private static readonly TextRect initial = new("Panel", 100, 100, 100, 30, 24, false);

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(3, 2, 4)]
    [InlineData(4, 3, 6)]
    [InlineData(5, 4, 8)]
    public void GeometryLevelControlsConfirmationIndependentlyOfRecognition(
        int level, int normalFrames, int microFrames)
    {
        foreach (int recognitionLevel in Enumerable.Range(1, 5))
        {
            TextRect normal = initial with
            {
                X = 120, Y = 110, Width = 115, Height = 35, FontSize = 28, Angle = 4, MultiLine = true,
            };
            TextRect micro = initial with
            {
                X = 102, Y = 102, Width = 102, Height = 32, FontSize = 25, Angle = 1, MultiLine = true,
            };
            foreach (var (changed, frames) in new[] { (normal, normalFrames), (micro, microFrames) })
            {
                OcrTextTracker tracker = Create(level, recognitionLevel);
                Update(tracker, 0, initial);
                for (int frame = 1; frame <= frames; frame++)
                {
                    Assert.Equal(frame < frames ? initial : changed, Assert.Single(Update(tracker, frame, changed)));
                }
            }
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    public void RecognitionLevelControlsTextAndSplitConfirmationIndependentlyOfGeometry(int level, int frames)
    {
        TextRect whole = new("Open Options And Configure", 100, 100, 300, 30, 24, false);
        TextRect changed = whole with { SourceText = "Open Settings And Configure" };
        TextRect[] split =
        [
            new("Open Settings", 100, 100, 150, 30, 24, false),
            new("And Configure", 250, 100, 150, 30, 24, false),
        ];
        foreach (int geometryLevel in Enumerable.Range(1, 5))
        {
            foreach (TextRect[] observations in new[] { new[] { changed }, split })
            {
                OcrTextTracker tracker = Create(geometryLevel, level);
                Update(tracker, 0, whole);
                for (int frame = 1; frame <= frames; frame++)
                {
                    TextRect result = Assert.Single(Update(tracker, frame, observations));
                    Assert.Equal(frame < frames ? whole.SourceText : changed.SourceText, result.SourceText);
                }
            }
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    public void RecognitionLevelControlsMergeAndRestoreIndependentlyOfGeometry(int level, int frames)
    {
        TextRect first = new("New", 50, 400, 70, 32, 25, false);
        TextRect second = new("Game", 125, 400, 90, 32, 25, false);
        TextRect merged = new("New Game", 50, 400, 165, 32, 25, false);
        foreach (int geometryLevel in Enumerable.Range(1, 5))
        {
            OcrTextTracker tracker = Create(geometryLevel, level);
            Update(tracker, 0, first, second);
            for (int frame = 1; frame <= frames; frame++)
            {
                Assert.Equal(frame < frames ? 2 : 1, Update(tracker, frame, merged).Count);
            }
            for (int frame = 1; frame <= frames; frame++)
            {
                IReadOnlyList<TextRect> result = Update(tracker, frames + frame, first, second);
                Assert.Equal(frame < frames ? [merged.SourceText] : new[] { first.SourceText, second.SourceText },
                    result.Select(rect => rect.SourceText));
            }
        }
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 4)]
    [InlineData(3, 5)]
    [InlineData(4, 6)]
    [InlineData(5, 8)]
    public void UnsettledTextEventuallyUsesTheLatestRecognition(int level, int historySize)
    {
        OcrTextTracker tracker = Create(recognition: level);
        Update(tracker, 0, initial);
        for (int frame = 1; frame <= historySize; frame++)
        {
            TextRect changed = initial with { SourceText = $"Panel{frame}" };
            TextRect result = Assert.Single(Update(tracker, frame, changed));
            Assert.Equal(level == 1 || frame == historySize ? changed.SourceText : initial.SourceText, result.SourceText);
        }
    }

    [Theory]
    [InlineData(4, 5)]
    [InlineData(5, 6)]
    public void ScatteredTextVotesNeedEnoughRecentWeight(int level, int pendingFrames)
    {
        OcrTextTracker tracker = Create(recognition: level);
        Update(tracker, 0, initial);
        string[] observations = ["PanelA", "PanelB", "PanelA", "PanelC", "PanelA", "PanelA", "PanelA"];
        for (int frame = 1; frame <= pendingFrames + 1; frame++)
        {
            TextRect result = Assert.Single(Update(tracker, frame, initial with { SourceText = observations[frame - 1] }));
            Assert.Equal(frame <= pendingFrames ? initial.SourceText : "PanelA", result.SourceText);
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void HigherGeometryStabilitySuppressesPeriodicPositionAndFontSizeChanges(int level)
    {
        // 2回ずつ続く振動は既定の確定回数を満たすが、レベル4・5では確定しない。
        TextRect jittered = initial with { X = 120, FontSize = 28 };
        OcrTextTracker defaults = Create();
        OcrTextTracker stable = Create(geometry: level);
        Update(defaults, 0, initial);
        Update(stable, 0, initial);
        bool defaultMoved = false;
        for (int frame = 1; frame <= 24; frame++)
        {
            TextRect observation = (frame - 1) % 4 < 2 ? jittered : initial;
            defaultMoved |= Assert.Single(Update(defaults, frame, observation)).X != initial.X;
            Assert.Equal(initial, Assert.Single(Update(stable, frame, observation)));
        }
        Assert.True(defaultMoved);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void HigherGeometryStabilitySuppressesPeriodicMicroGeometryChanges(int level)
    {
        // 4観測中3回の微小なずれは既定の中央値を動かすが、長い観測窓では安定値が優勢になる。
        OcrTextTracker defaults = Create();
        OcrTextTracker stable = Create(geometry: level);
        Update(defaults, 0, initial);
        Update(stable, 0, initial);
        bool defaultMoved = false;
        for (int frame = 1; frame <= 48; frame++)
        {
            TextRect observation = (frame - 1) % 24 < 3 ? initial with { X = 102, FontSize = 25 } : initial;
            defaultMoved |= Assert.Single(Update(defaults, frame, observation)).X != initial.X;
            Assert.Equal(initial, Assert.Single(Update(stable, frame, observation)));
        }
        Assert.True(defaultMoved);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void LargeMovementIsImmediateAtEveryGeometryLevel(int level)
    {
        OcrTextTracker tracker = Create(geometry: level);
        Update(tracker, 0, initial);
        TextRect moved = initial with { X = 180 };
        Assert.Equal(moved, Assert.Single(Update(tracker, 1, moved)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void RetentionCountsConsecutiveMissesAndResetsOnRecognition(int retention)
    {
        OcrTextTracker tracker = Create(retention: retention);
        Update(tracker, 0, initial);
        for (int frame = 1; frame <= retention; frame++)
        {
            // 実行間隔に依存しないことも確認する。
            Assert.Single(Update(tracker, frame * 20));
        }
        Assert.Single(Update(tracker, 200, initial));
        for (int frame = 1; frame <= retention; frame++)
        {
            Assert.Single(Update(tracker, 200 + frame));
        }
        Assert.Empty(Update(tracker, 201 + retention));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public void RetentionDoesNotChangeAssignmentRecency(int retention)
    {
        OcrTextTracker tracker = Create(retention: retention);
        TextRect missed = initial with { SourceText = "PanelA" };
        TextRect recent = initial with { SourceText = "PanelB", X = 104 };
        Update(tracker, 0, missed, recent);
        Update(tracker, 1, recent);

        // 位置は欠落側に近いが、固定のrecency基準では直前に認識した側が優先される。
        TextRect ambiguous = initial with { SourceText = "PanelC", X = 101, Context = "matched" };
        TextRect result = Assert.Single(Update(tracker, 2, ambiguous), rect => rect.Context == "matched");
        Assert.Equal(recent.SourceText, result.SourceText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(7)]
    public void DormantIdentityRetentionRemainsFiveSeconds(int retention)
    {
        TextRect first = new("New", 50, 400, 70, 32, 25, false);
        TextRect second = new("Game", 125, 400, 90, 32, 25, false);
        TextRect merged = new("New Game", 50, 400, 165, 32, 25, false);
        foreach (bool expired in new[] { false, true })
        {
            OcrTextTracker tracker = Create(recognition: 1, retention: retention);
            Update(tracker, 0, first, second);
            Update(tracker, 1, merged);
            Update(tracker, expired ? 12 : 10, merged);
            IReadOnlyList<TextRect> result = Update(tracker, expired ? 13 : 11, first, second);
            Assert.Equal(expired ? [merged.SourceText] : new[] { first.SourceText, second.SourceText },
                result.Select(rect => rect.SourceText));
        }
    }

    [Fact]
    public void ExistingSettingsUseDefaultsAndAppSettingsRoundTripIndependently()
    {
        UserSettings old = JsonSerializer.Deserialize<UserSettings>("""{"Targets":{"OldApp":{}}}""")!;
        TargetSettings defaults = old.Targets["OldApp"];
        Assert.Equal((3, 3, 3), (defaults.OcrGeometryStability, defaults.OcrRecognitionStability, defaults.OcrMissingFrameRetention));
        var settings = new UserSettings
        {
            Targets = new()
            {
                ["First"] = new() { OcrGeometryStability = 5, OcrRecognitionStability = 1, OcrMissingFrameRetention = 0 },
                ["Second"] = new() { OcrGeometryStability = 2, OcrRecognitionStability = 4, OcrMissingFrameRetention = 7 },
            },
        };
        UserSettings restored = JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings))!;
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        foreach (var (name, target) in restored.Targets)
        {
            TargetSettingsViewModel viewModel = new(name, services, target, [], [], []);
            TargetSettings expected = settings.Targets[name];
            Assert.Equal((expected.OcrGeometryStability, expected.OcrRecognitionStability, expected.OcrMissingFrameRetention),
                (viewModel.OcrGeometryStability, viewModel.OcrRecognitionStability, viewModel.OcrMissingFrameRetention));
        }
    }

    [Theory]
    [InlineData(-10, 1, 0)]
    [InlineData(3, 3, 3)]
    [InlineData(10, 5, 7)]
    public void SettingsLoadedFromConfigurationStayWithinSupportedRanges(int value, int level, int retention)
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [nameof(TargetSettings.OcrGeometryStability)] = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [nameof(TargetSettings.OcrRecognitionStability)] = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [nameof(TargetSettings.OcrMissingFrameRetention)] = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }).Build();
        TargetSettings settings = config.Get<TargetSettings>()!;
        Assert.Equal((level, level, retention),
            (settings.OcrGeometryStability, settings.OcrRecognitionStability, settings.OcrMissingFrameRetention));
    }

    private static OcrTextTracker Create(int geometry = 3, int recognition = 3, int retention = 3)
        => new(NullLogger<OcrTextTracker>.Instance, new()
        {
            OcrGeometryStability = geometry,
            OcrRecognitionStability = recognition,
            OcrMissingFrameRetention = retention,
        });

    private static IReadOnlyList<TextRect> Update(OcrTextTracker tracker, int frame, params TextRect[] observations)
        => tracker.Update(observations, imageSize, TimeSpan.FromMilliseconds(frame * 500));
}
