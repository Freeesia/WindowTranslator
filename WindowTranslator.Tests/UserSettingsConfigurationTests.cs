using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WindowTranslator.Modules;
using WindowTranslator.Stores;

namespace WindowTranslator.Tests;

public sealed class UserSettingsConfigurationTests
{
    [Fact]
    public void UserSettingsIgnoresPluginParametersThatHaveNoLoadedType()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Common:ViewMode"] = nameof(ViewMode.Capture),
                ["Targets::Font"] = "Default Font",
                ["Targets:game:Font"] = "Test Font",
                ["Targets:game:SelectedPlugins:ITranslateModule"] = "MissingTranslator",
                ["Targets:game:PluginParams:MissingOptions:ApiKey"] = "secret",
            })
            .Build();
        var settings = new UserSettings();

        new global::ConfigureUserSettings(configuration).Configure(settings);

        Assert.Equal(ViewMode.Capture, settings.Common.ViewMode);
        Assert.Equal("Default Font", settings.Targets[string.Empty].Font);
        var target = settings.Targets["game"];
        Assert.Equal("Test Font", target.Font);
        Assert.Equal(
            "MissingTranslator",
            target.SelectedPlugins[nameof(ITranslateModule)]);
        Assert.Empty(target.PluginParams);
    }

    [Fact]
    public void InvalidLoadedPluginParameterIsIgnoredWithoutChangingDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Targets:game:PluginParams:InvalidPluginParam:RetryCount"] =
                    "not-an-integer",
            })
            .Build();
        var options = new InvalidPluginParam();
        var configure = new global::ConfigurePluginParam<InvalidPluginParam>(
            configuration,
            new TestProcessInfoStore("game"),
            NullLogger<global::ConfigurePluginParam<InvalidPluginParam>>.Instance);

        configure.Configure(options);

        Assert.Equal(7, options.RetryCount);
    }

    public sealed class InvalidPluginParam : IPluginParam
    {
        public int RetryCount { get; set; } = 7;
    }

    private sealed class TestProcessInfoStore(string name) : IProcessInfoStore
    {
        public IntPtr MainWindowHandle => IntPtr.Zero;

        public string Name { get; } = name;
    }
}
