extern alias DeepLPlugin;
extern alias GoogleAIPlugin;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;
using WindowTranslator.Stores;
using DeepLOptions = DeepLPlugin::WindowTranslator.Plugin.DeepLTranslatePlugin.DeepLOptions;
using GoogleAIOptions = GoogleAIPlugin::WindowTranslator.Plugin.GoogleAIPlugin.GoogleAIOptions;
using GoogleAIValidator = GoogleAIPlugin::WindowTranslator.Plugin.GoogleAIPlugin.GoogleAIValidator;

namespace WindowTranslator.Tests;

public class TargetSettingsConfigurationTests
{
    [Fact]
    public async Task SavedPluginKeysAreRestoredForStartupValidation()
    {
        using var provider = CreateProvider();
        var settings = provider.GetRequiredService<IOptionsSnapshot<TargetSettings>>().Get("game");

        Assert.Equal("deepl-game", Assert.IsType<DeepLOptions>(settings.PluginParams[nameof(DeepLOptions)]).AuthKey);
        Assert.Equal("gemini-game", Assert.IsType<GoogleAIOptions>(settings.PluginParams[nameof(GoogleAIOptions)]).ApiKey);
        Assert.True((await new GoogleAIValidator().Validate(settings)).IsValid);
    }

    [Fact]
    public void MissingTargetUsesDefaultPluginParameters()
    {
        using var provider = CreateProvider();
        var settings = provider.GetRequiredService<IOptionsSnapshot<TargetSettings>>().Get("unknown");

        Assert.Equal("deepl-default", Assert.IsType<DeepLOptions>(settings.PluginParams[nameof(DeepLOptions)]).AuthKey);
        Assert.Equal("gemini-default", Assert.IsType<GoogleAIOptions>(settings.PluginParams[nameof(GoogleAIOptions)]).ApiKey);
    }

    [Fact]
    public async Task ExistingTargetWithoutKeyDoesNotInheritAnotherTargetsKey()
    {
        using var provider = CreateProvider();
        var settings = provider.GetRequiredService<IOptionsSnapshot<TargetSettings>>().Get("empty");

        Assert.True(string.IsNullOrEmpty(Assert.IsType<DeepLOptions>(settings.PluginParams[nameof(DeepLOptions)]).AuthKey));
        Assert.True(string.IsNullOrEmpty(Assert.IsType<GoogleAIOptions>(settings.PluginParams[nameof(GoogleAIOptions)]).ApiKey));
        Assert.False((await new GoogleAIValidator().Validate(settings)).IsValid);
    }

    [Fact]
    public void NamedSettingsHaveIndependentPluginParameterInstances()
    {
        using var provider = CreateProvider();
        var options = provider.GetRequiredService<IOptionsSnapshot<TargetSettings>>();
        var game = Assert.IsType<DeepLOptions>(options.Get("game").PluginParams[nameof(DeepLOptions)]);
        var defaults = Assert.IsType<DeepLOptions>(options.Get("unknown").PluginParams[nameof(DeepLOptions)]);

        game.AuthKey = "edited";

        Assert.NotSame(game, defaults);
        Assert.Equal("deepl-default", defaults.AuthKey);
    }

    [Fact]
    public void UnnamedSettingsUseCurrentProcessAndPreserveLegacyOcrParameters()
    {
        using var provider = CreateProvider();
        var settings = provider.GetRequiredService<IOptionsSnapshot<TargetSettings>>().Value;

        Assert.Equal("deepl-game", Assert.IsType<DeepLOptions>(settings.PluginParams[nameof(DeepLOptions)]).AuthKey);
        var ocr = Assert.IsType<BasicOcrParam>(settings.PluginParams[nameof(BasicOcrParam)]);
        Assert.Equal(0.75, ocr.Scale);
    }

    private static ServiceProvider CreateProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Targets::PluginParams:DeepLOptions:AuthKey"] = "deepl-default",
            ["Targets::PluginParams:GoogleAIOptions:ApiKey"] = "gemini-default",
            ["Targets:game:SelectedPlugins:ITranslateModule"] = "GoogleAITranslator",
            ["Targets:game:PluginParams:DeepLOptions:AuthKey"] = "deepl-game",
            ["Targets:game:PluginParams:GoogleAIOptions:ApiKey"] = "gemini-game",
            ["Targets:game:PluginParams:WindowsMediaOcrParam:Scale"] = "0.75",
            ["Targets:empty:SelectedPlugins:ITranslateModule"] = "GoogleAITranslator",
        }).Build();
        var store = new ProcessInfoStore();
        store.SetTargetProcess(IntPtr.Zero, "game");
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IProcessInfoStore>(store);
        services.AddTransient<IPluginParam, DeepLOptions>();
        services.AddTransient<IPluginParam, GoogleAIOptions>();
        services.AddTransient<IPluginParam, BasicOcrParam>();
        services.AddTransient(typeof(IConfigureNamedOptions<>), typeof(ConfigurePluginParam<>));
        services.AddTransient(typeof(IConfigureOptions<>), typeof(ConfigurePluginParam<>));
        services.AddTransient<IConfigureOptions<TargetSettings>, ConfigureTargetSettings>();
        return services.BuildServiceProvider();
    }
}
