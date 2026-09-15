using System.Globalization;
using Microsoft.Extensions.Configuration;
using WindowTranslator.ComponentModel;
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

    [Theory]
    [InlineData("ja-JP", "https://wt.studiofreesia.com/OcrModule")]
    [InlineData("en-US", "https://wt.studiofreesia.com/OcrModule.en")]
    [InlineData("zh-CN", "https://wt.studiofreesia.com/OcrModule.zh-cn")]
    [InlineData("zh-TW", "https://wt.studiofreesia.com/OcrModule.zh-tw")]
    public void HelpUriUsesTheUiLanguage(string cultureName, string expected)
    {
        var actual = HelpUriBuilder.Build("OcrModule", CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("OcrModule", 1)]
    [InlineData("TranslateModule", 1)]
    [InlineData("SetupTranslationOcr", 2)]
    [InlineData("SetupOther", 0)]
    public void SetupGroupProvidesOnlyRelevantHelpLinks(string categoryKey, int expectedCount)
    {
        var group = new PluginSetupGroup(categoryKey, []);

        Assert.Equal(expectedCount, group.HelpLinks.Count);
    }

    [Fact]
    public void TranslationOcrGroupLinksBothDocuments()
    {
        var group = new PluginSetupGroup("SetupTranslationOcr", []);

        Assert.Collection(
            group.HelpLinks,
            link => Assert.Contains("TranslateModule", link.Uri, StringComparison.Ordinal),
            link => Assert.Contains("OcrModule", link.Uri, StringComparison.Ordinal));
    }

    private static NuGetPackageInfo CreatePackage(string id)
        => new(id, id, string.Empty, "Freesia", null, null, ["1.0.0"], IsOfficial: true);
}
