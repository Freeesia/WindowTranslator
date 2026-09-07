using Microsoft.Extensions.Configuration;
using WindowTranslator.Modules.PluginStore;

namespace WindowTranslator.Tests;

public sealed class PluginSetupTests
{
    [Fact]
    public void NewUserHasNoInitialPluginSelection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var package = new PluginSetupPackage(CreatePackage("WindowTranslator.Plugin.OneOcrPlugin"), configuration);

        Assert.False(package.IsSelected);
    }

    [Fact]
    public void ExistingSelectedPluginIsInitiallySelected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Targets:Game:SelectedPlugins:IOcrModule"] = "OneOcr",
            })
            .Build();

        var package = new PluginSetupPackage(CreatePackage("WindowTranslator.Plugin.OneOcrPlugin"), configuration);

        Assert.True(package.IsSelected);
    }

    [Fact]
    public void EnabledCorrectionPluginIsInitiallySelected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Targets:Game:PluginParams:LLMOptions:CorrectMode"] = "Image",
            })
            .Build();

        var package = new PluginSetupPackage(CreatePackage("WindowTranslator.Plugin.LLMPlugin"), configuration);

        Assert.True(package.IsSelected);
    }

    private static NuGetPackageInfo CreatePackage(string id)
        => new(id, id, string.Empty, "Freesia", null, null, ["1.0.0"], IsOfficial: true);
}
