using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules.Main;

namespace WindowTranslator.Tests;

public class PluginValidationSettingsTests
{
    [Fact]
    public void RuntimeValidationUsesNamedPluginParameters()
    {
        var services = new ServiceCollection();
        services.AddTransient<IPluginParam, TestPluginParam>();
        services.AddTransient<IConfigureNamedOptions<TestPluginParam>, ConfigureTestPluginParam>();
        using var provider = services.BuildServiceProvider();
        var settings = new TargetSettings();

        MainWindowModule.ConfigurePluginParams(provider, "game", settings);

        var param = Assert.IsType<TestPluginParam>(settings.PluginParams[nameof(TestPluginParam)]);
        Assert.Equal("game-key", param.ApiKey);
    }

    private sealed class TestPluginParam : IPluginParam
    {
        public string? ApiKey { get; set; }
    }

    private sealed class ConfigureTestPluginParam : IConfigureNamedOptions<TestPluginParam>
    {
        public void Configure(TestPluginParam options)
            => Configure(Options.DefaultName, options);

        public void Configure(string? name, TestPluginParam options)
            => options.ApiKey = $"{name}-key";
    }
}
