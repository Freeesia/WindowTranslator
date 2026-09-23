using System.Globalization;
using WindowTranslator.Modules.PluginStore;

namespace WindowTranslator.Tests;

public class LocalizedReadmeSelectorTests
{
    [Theory]
    [InlineData("pt-BR", "Português do Brasil")]
    [InlineData("fr-CA", "Français")]
    [InlineData("it-IT", "English")]
    public void SelectUsesExactParentAndEnglishFallback(
        string cultureName,
        string expected)
    {
        const string markdown = """
            ## pt-BR

            Português do Brasil

            ## fr

            Français

            ## en

            English
            """;

        var result = LocalizedReadmeSelector.Select(
            markdown,
            CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SelectKeepsTheSharedPreambleAndOrdinaryHeadings()
    {
        const string markdown = """
            # Shared title

            Shared introduction.

            ## ja

            日本語

            ## 機能

            - 機能A

            ## en

            English

            ## Features

            - Feature A
            """;

        var result = LocalizedReadmeSelector.Select(
            markdown,
            CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Contains("# Shared title", result);
        Assert.Contains("## 機能", result);
        Assert.Contains("- 機能A", result);
        Assert.DoesNotContain("## ja", result);
        Assert.DoesNotContain("English", result);
    }

    [Fact]
    public void SelectFallsBackToTheFirstNonEmptyLanguageSection()
    {
        const string markdown = """
            ## ja

            日本語

            ## de

            Deutsch
            """;

        var result = LocalizedReadmeSelector.Select(
            markdown,
            CultureInfo.GetCultureInfo("fr-FR"));

        Assert.Equal("日本語", result);
    }

    [Fact]
    public void SelectReturnsTheOriginalMarkdownWithoutLanguageSections()
    {
        const string markdown = """
            # README

            ## Features

            - Feature A
            """;

        var result = LocalizedReadmeSelector.Select(
            markdown,
            CultureInfo.GetCultureInfo("en-US"));

        Assert.Same(markdown, result);
    }

    [Fact]
    public void SelectKeepsAnUppercaseAcronymHeadingInsideTheLanguageSection()
    {
        const string markdown = """
            ## ja

            日本語

            ## API

            APIの説明

            ## en

            English
            """;

        var result = LocalizedReadmeSelector.Select(
            markdown,
            CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Contains("## API", result);
        Assert.Contains("APIの説明", result);
        Assert.DoesNotContain("English", result);
    }

}
