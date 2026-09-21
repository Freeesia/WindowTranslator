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

        var package = new PluginSetupPackage(CreatePackage("WindowTranslator.Plugin.TesseractOCRPlugin"), configuration);

        Assert.False(package.IsSelected);
    }

    [Fact]
    public void ExistingSelectedPluginIsInitiallySelected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Targets:Game:SelectedPlugins:IOcrModule"] = "TesseractOcr",
            })
            .Build();

        var package = new PluginSetupPackage(CreatePackage("WindowTranslator.Plugin.TesseractOCRPlugin"), configuration);

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
    [InlineData("WindowTranslator.Plugin.GoogleAIPlugin", "GoogleAITranslator", "GoogleAIOptions")]
    [InlineData("WindowTranslator.Plugin.LLMPlugin", "LLMTranslator", "LLMOptions")]
    public void AiPluginsAreTranslationItemsAndKeepExistingSelections(
        string id, string translator, string options)
    {
        var packageInfo = CreatePackage(id, tags: ["translate", "filter"]);
        var selectedTranslator = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Targets:Game:SelectedPlugins:ITranslateModule"] = translator,
        }).Build();
        var selectedCorrection = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"Targets:Game:PluginParams:{options}:CorrectMode"] = "Text",
        }).Build();

        var translation = new PluginSetupPackage(packageInfo, selectedTranslator);
        var correction = new PluginSetupPackage(packageInfo, selectedCorrection);

        Assert.Equal("TranslateModule", translation.CategoryKey);
        Assert.Equal(id, translation.Package.Title);
        Assert.True(translation.IsSelected);
        Assert.True(correction.IsSelected);
    }

    [Theory]
    [InlineData("translate;filter", "TranslateModule")]
    [InlineData("ocr;filter", "OcrModule")]
    [InlineData("filter", "PluginCategoryFilter")]
    [InlineData("other", "SetupOther")]
    public void SetupUsesPackageTagsAndTitle(string tags, string category)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        const string title = "Publisher-provided package title";

        var package = new PluginSetupPackage(CreatePackage("Unknown.Plugin", title,
            tags.Split(';')), configuration);

        Assert.Equal(category, package.CategoryKey);
        Assert.Equal(title, package.Package.Title);
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
    [InlineData("PluginCategoryFilter", 0)]
    [InlineData("SetupOther", 0)]
    public void SetupGroupProvidesOnlyRelevantHelpLinks(string categoryKey, int expectedCount)
    {
        var group = new PluginSetupGroup(categoryKey, []);

        Assert.Equal(expectedCount, group.HelpLinks.Count);
    }

    [Fact]
    public void AiPluginsLinkOnlyTranslationDocument()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var packages = new[] { "WindowTranslator.Plugin.GoogleAIPlugin", "WindowTranslator.Plugin.LLMPlugin" }
            .Select(id => new PluginSetupPackage(CreatePackage(id, tags: ["translate", "filter"]), configuration)).ToArray();
        var group = new PluginSetupGroup("TranslateModule", packages);

        Assert.All(packages, package => Assert.Equal(group.CategoryKey, package.CategoryKey));
        Assert.Contains("TranslateModule", Assert.Single(group.HelpLinks).Uri, StringComparison.Ordinal);
    }

    private static NuGetPackageInfo CreatePackage(string id, string? title = null, IReadOnlyList<string>? tags = null)
        => new(id, title ?? id, string.Empty, "Freesia", null, null, ["1.0.0"], IsOfficial: true)
        {
            Tags = tags ?? [],
        };
}
