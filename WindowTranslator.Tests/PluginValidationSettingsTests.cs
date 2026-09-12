using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WindowTranslator.Stores;

namespace WindowTranslator.Tests;

public class PluginValidationSettingsTests
{
    [Fact]
    public void CurrentTargetSettingsResolvePluginParametersThroughOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Targets:game:PluginParams:TestPluginParam:ApiKey"] = "game-key",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IProcessInfoStore>(new TestProcessInfoStore("game"));
        services.AddTransient<IPluginParam, TestPluginParam>();
        services.AddTransient(typeof(IConfigureNamedOptions<>), typeof(global::ConfigurePluginParam<>));
        services.AddTransient(typeof(IConfigureOptions<>), typeof(global::ConfigurePluginParam<>));
        services.AddTransient<IConfigureNamedOptions<TargetSettings>, global::ConfigureTargetSettings>();
        services.AddTransient<IConfigureOptions<TargetSettings>, global::ConfigureTargetSettings>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var settings = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TargetSettings>>().Value;

        var param = Assert.IsType<TestPluginParam>(settings.PluginParams[nameof(TestPluginParam)]);
        Assert.Equal("game-key", param.ApiKey);
    }

    private sealed class TestPluginParam : IPluginParam
    {
        public string? ApiKey { get; set; }
    }

    private sealed class TestProcessInfoStore(string name) : IProcessInfoStore
    {
        public IntPtr MainWindowHandle => IntPtr.Zero;

        public string Name => name;
    }
}
