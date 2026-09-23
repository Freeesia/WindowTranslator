namespace WindowTranslator.Tests;

public class WindowMonitorTests
{
    [Fact]
    public void ShouldAutoAttach_UsesOnlyTheNamedTargetSetting()
    {
        var settings = new UserSettings()
        {
            Targets =
            {
                [string.Empty] = new() { IsEnableAutoTarget = true },
                ["EnabledApp"] = new() { IsEnableAutoTarget = true },
                ["DisabledApp"] = new() { IsEnableAutoTarget = false },
            },
        };

        Assert.True(WindowMonitor.ShouldAutoAttach(settings, "enabledapp"));
        Assert.False(WindowMonitor.ShouldAutoAttach(settings, "DisabledApp"));
        Assert.False(WindowMonitor.ShouldAutoAttach(settings, "UnknownApp"));
    }

    [Fact]
    public void ShouldAutoAttach_UsesTheLatestTargetSetting()
    {
        var settings = new UserSettings()
        {
            Targets =
            {
                ["App"] = new() { IsEnableAutoTarget = false },
            },
        };

        Assert.False(WindowMonitor.ShouldAutoAttach(settings, "App"));

        settings.Targets["App"].IsEnableAutoTarget = true;

        Assert.True(WindowMonitor.ShouldAutoAttach(settings, "App"));
    }
}
