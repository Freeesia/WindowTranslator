using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WindowTranslator.Stores;
using WindowTranslator.Modules;

namespace WindowTranslator.Tests;

public class ConfigurePluginParamOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("game")]
    public void MissingPluginConfigurationUsesPluginDefaults(string? target)
    {
        var values = new Dictionary<string, string?>();
        if (target is not null)
        {
            values[$"Targets:{target}:SelectedPlugins:ITranslateModule"] = "TestTranslator";
        }
        using var provider = CreateProvider(values, target ?? "");
        using var scope = provider.CreateScope();

        var param = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestPluginParam>>().Get(target ?? "");

        Assert.Equal("initial-key", param.ApiKey);
    }

    [Fact]
    public void ConcretePluginOptionsUseTheSameNamedConfiguration()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Targets:game:PluginParams:TestPluginParam:ApiKey"] = "game-key",
        }, "game");
        using var scope = provider.CreateScope();

        var param = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestPluginParam>>().Get("game");

        Assert.Equal("game-key", param.ApiKey);
    }

    [Theory]
    [InlineData("missing", "", "default-key")]
    [InlineData("", "game", "game-key")]
    public void ConcretePluginOptionsUseDefaultOrCurrentTarget(
        string name, string currentTarget, string expectedApiKey)
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Targets::PluginParams:TestPluginParam:ApiKey"] = "default-key",
            ["Targets:game:PluginParams:TestPluginParam:ApiKey"] = "game-key",
        }, currentTarget);
        using var scope = provider.CreateScope();
        var param = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestPluginParam>>().Get(name);
        Assert.Equal(expectedApiKey, param.ApiKey);
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> values, string currentTarget = "")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IProcessInfoStore>(new TestProcessInfoStore(currentTarget));
        services.AddTransient(typeof(IConfigureNamedOptions<>), typeof(global::ConfigurePluginParam<>));
        services.AddTransient(typeof(IConfigureOptions<>), typeof(global::ConfigurePluginParam<>));
        return services.BuildServiceProvider();
    }

    private sealed class TestPluginParam : IPluginParam
    {
        public string? ApiKey { get; set; } = "initial-key";
    }

    private sealed class TestProcessInfoStore(string name) : IProcessInfoStore
    {
        public IntPtr MainWindowHandle => IntPtr.Zero;

        public string Name => name;
    }
}
