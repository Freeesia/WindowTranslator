using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WindowTranslator.Modules.Capture;

namespace WindowTranslator.Tests;

public class CaptureIntervalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.1)]
    [InlineData(1)]
    [InlineData(5)]
    public void FirstCaptureStartsImmediately(double seconds)
    {
        var interval = new CaptureIntervalController(seconds, new TestTimeProvider());

        Assert.True(interval.CanStart);
    }

    [Fact]
    public void ZeroIntervalAddsNoWaitAfterProcessing()
    {
        var clock = new TestTimeProvider();
        var interval = new CaptureIntervalController(0, clock);
        clock.Advance(TimeSpan.FromSeconds(2));

        interval.Complete();

        Assert.True(interval.CanStart);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(5)]
    public void WaitStartsAfterProcessingCompletes(double seconds)
    {
        var clock = new TestTimeProvider();
        var interval = new CaptureIntervalController(seconds, clock);

        // OCRなどに2秒かかっても、待機秒数は処理完了から測る。
        clock.Advance(TimeSpan.FromSeconds(2));
        interval.Complete();
        Assert.False(interval.CanStart);
        clock.Advance(TimeSpan.FromSeconds(seconds) - TimeSpan.FromTicks(1));
        Assert.False(interval.CanStart);
        clock.Advance(TimeSpan.FromTicks(1));
        Assert.True(interval.CanStart);

        // 次の処理が完了したら、再び指定された秒数を待つ。
        clock.Advance(TimeSpan.FromSeconds(2));
        interval.Complete();
        Assert.False(interval.CanStart);
    }

    [Fact]
    public void ResumingCaptureStartsImmediately()
    {
        var interval = new CaptureIntervalController(5, new TestTimeProvider());
        interval.Complete();
        Assert.False(interval.CanStart);

        interval.Reset();

        Assert.True(interval.CanStart);
    }

    [Fact]
    public void TargetsHaveIndependentCaptureIntervals()
    {
        var clock = new TestTimeProvider();
        var first = new CaptureIntervalController(1, clock);
        var second = new CaptureIntervalController(3, clock);
        first.Complete();
        second.Complete();

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.True(first.CanStart);
        Assert.False(second.CanStart);
    }

    [Fact]
    public void ExistingSettingsHaveNoWaitAndFractionalSecondsRoundTripPerTarget()
    {
        var existing = JsonSerializer.Deserialize<UserSettings>("""{"Targets":{"OldApp":{}}}""")!;
        Assert.Equal(0, existing.Targets["OldApp"].CaptureInterval);
        var settings = new UserSettings
        {
            Targets = new()
            {
                ["First"] = new() { CaptureInterval = 0.25 },
                ["Second"] = new() { CaptureInterval = 3 },
            },
        };

        var restored = JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings))!;

        Assert.Equal(0.25, restored.Targets["First"].CaptureInterval);
        Assert.Equal(3, restored.Targets["Second"].CaptureInterval);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.25, 0.25)]
    [InlineData(3, 3)]
    public void ConfigurationLoadsSecondsAsANonNegativeNumber(double value, double expected)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [nameof(TargetSettings.CaptureInterval)] = value.ToString(CultureInfo.InvariantCulture),
        }).Build();

        var settings = configuration.Get<TargetSettings>()!;

        Assert.Equal(expected, settings.CaptureInterval);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => this.timestamp;

        public void Advance(TimeSpan duration) => this.timestamp += duration.Ticks;
    }
}
