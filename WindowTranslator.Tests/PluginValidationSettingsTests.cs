using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WindowTranslator.Stores;
using WindowTranslator.Modules;

namespace WindowTranslator.Tests;

public class PluginValidationSettingsTests
{
    [Fact]
    public void NamedTargetSettingsUsesConfiguredPluginParams()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Targets:other:PluginParams:TestPluginParam:ApiKey"] = "other-key",
            ["Targets:game:PluginParams:TestPluginParam:ApiKey"] = "game-key",
        });
        using var scope = provider.CreateScope();

        var settings = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TargetSettings>>().Get("game");

        var param = Assert.IsType<TestPluginParam>(settings.PluginParams[nameof(TestPluginParam)]);
        Assert.Equal("game-key", param.ApiKey);
    }

    [Fact]
    public void NamedTargetSettingsDoNotSharePluginParams()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Targets:first:PluginParams:TestPluginParam:ApiKey"] = "first-key",
            ["Targets:second:PluginParams:TestPluginParam:ApiKey"] = "second-key",
        });
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TargetSettings>>();

        var first = Assert.IsType<TestPluginParam>(options.Get("first").PluginParams[nameof(TestPluginParam)]);
        var second = Assert.IsType<TestPluginParam>(options.Get("second").PluginParams[nameof(TestPluginParam)]);

        Assert.Equal("first-key", first.ApiKey);
        Assert.Equal("second-key", second.ApiKey);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ConcretePluginOptionsUseTheSameNamedConfiguration()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Targets:game:PluginParams:TestPluginParam:ApiKey"] = "game-key",
        }, "game");
        using var scope = provider.CreateScope();

        var param = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestPluginParam>>().Value;

        Assert.Equal("game-key", param.ApiKey);
    }

    [Theory]
    [InlineData("missing", "")]
    [InlineData("", "game")]
    public void TargetSettingsFollowDefaultAndCurrentTarget(string name, string currentTarget)
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            ["Targets::PluginParams:TestPluginParam:ApiKey"] = "default-key",
            ["Targets:game:PluginParams:TestPluginParam:ApiKey"] = "game-key",
        }, currentTarget);
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TargetSettings>>();
        var param = Assert.IsType<TestPluginParam>(options.Get(name).PluginParams[nameof(TestPluginParam)]);
        Assert.Equal(currentTarget == "game" ? "game-key" : "default-key", param.ApiKey);
    }

    [Theory]
    [InlineData("WindowsMediaOcrParam")]
    [InlineData("BasicOcrParam")]
    public void InterfaceAndConcreteOptionsUseOcrCompatibilitySection(string sectionName)
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            [$"Targets:game:PluginParams:{sectionName}:Scale"] = "2",
        });
        using var scope = provider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TargetSettings>>().Get("game");
        var param = Assert.IsType<BasicOcrParam>(settings.PluginParams[nameof(BasicOcrParam)]);
        var concrete = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<BasicOcrParam>>().Get("game");
        Assert.Equal(2, param.Scale);
        Assert.Equal(concrete.Scale, param.Scale);
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
        services.AddTransient<IPluginParam, TestPluginParam>();
        services.AddTransient<IPluginParam, BasicOcrParam>();
        services.AddTransient(typeof(IConfigureNamedOptions<>), typeof(global::ConfigurePluginParam<>));
        services.AddTransient(typeof(IConfigureOptions<>), typeof(global::ConfigurePluginParam<>));
        services.AddTransient<IConfigureNamedOptions<TargetSettings>, global::ConfigureTargetSettings>();
        services.AddTransient<IConfigureOptions<TargetSettings>, global::ConfigureTargetSettings>();
        return services.BuildServiceProvider();
    }

    private sealed class TestPluginParam : IPluginParam
    {
        public string? ApiKey { get; set; }
    }

    private sealed class TestProcessInfoStore(string name) : IProcessInfoStore
    {
        public IntPtr TargetHandle => IntPtr.Zero;

        public string Name => name;
    }
}
