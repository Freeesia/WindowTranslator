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
    [InlineData("WindowTranslator.Plugin.GoogleAIPlugin", "Gemini", "GoogleAITranslator", "GoogleAIOptions")]
    [InlineData("WindowTranslator.Plugin.LLMPlugin", "LLM", "LLMTranslator", "LLMOptions")]
    public void AiPluginsAreTranslationItemsAndKeepExistingSelections(
        string id, string name, string translator, string options)
    {
        var packageInfo = CreatePackage(id);
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
        Assert.StartsWith(name, translation.DisplayName, StringComparison.Ordinal);
        Assert.True(translation.IsSelected);
        Assert.True(correction.IsSelected);
    }

    [Theory]
    [InlineData("WindowTranslator.Plugin.DeepLTranslatePlugin", "TranslateModule", "DeepL")]
    [InlineData("WindowTranslator.Plugin.OrcaRouterPlugin", "TranslateModule", "OrcaRouter")]
    [InlineData("WindowTranslator.Plugin.TesseractOCRPlugin", "OcrModule", "Tesseract OCR")]
    [InlineData("WindowTranslator.Plugin.FoMPlugin", "PluginCategoryFilter", "Fields of Mistria")]
    public void SetupUsesPurposeAndConciseName(string id, string category, string name)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var package = new PluginSetupPackage(CreatePackage(id), configuration);

        Assert.Equal(category, package.CategoryKey);
        Assert.Equal(name, package.DisplayName);
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
            .Select(id => new PluginSetupPackage(CreatePackage(id), configuration)).ToArray();
        var group = new PluginSetupGroup("TranslateModule", packages);

        Assert.All(packages, package => Assert.Equal(group.CategoryKey, package.CategoryKey));
        Assert.Contains("TranslateModule", Assert.Single(group.HelpLinks).Uri, StringComparison.Ordinal);
    }

    private static NuGetPackageInfo CreatePackage(string id)
        => new(id, id, string.Empty, "Freesia", null, null, ["1.0.0"], IsOfficial: true);
}
