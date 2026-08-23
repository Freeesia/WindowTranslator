using System.Globalization;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

/// <summary>
/// OCR対象範囲UIのリソースに関するテスト
/// </summary>
public class PriorityRectResourceTests
{
    private static CustomResourceManager CreateResourceManager()
        => new("WindowTranslator.Properties.Resources", typeof(OcrTextTracker).Assembly);

    [Theory]
    [InlineData("PriorityRectKeyword", "Keyword")]
    [InlineData("PriorityRectSelection", "Rectangle Selection")]
    [InlineData("PriorityRectSelectionGuide", "Drag to select a rectangle (press Esc to cancel)\nSelect a slightly wider area so that text is not cut off")]
    [InlineData("PriorityRectSelecting", "Selecting")]
    [InlineData("PriorityRectTooSmall", "The rectangle is too small. Please select again.")]
    [InlineData("PriorityRectTargetNotFound", "No window is being translated, so a rectangle cannot be selected. Start translating the target window before configuring.")]
    public void 翻訳がない場合は英語へフォールバックする(string key, string expected)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
            var resources = CreateResourceManager();
            Assert.Equal(
                expected.ReplaceLineEndings("\n"),
                resources.GetString(key, CultureInfo.CurrentUICulture)?.ReplaceLineEndings("\n"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("PriorityRectAdd", "Add range")]
    [InlineData("PriorityRectKeywordDescription", "Used by translation modules that support context")]
    public void 変更した英語メッセージが取得できる(string key, string expected)
        => Assert.Equal(expected, CreateResourceManager().GetString(key, CultureInfo.GetCultureInfo("en")));

    [Theory]
    [InlineData("ar")]
    [InlineData("cs")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fa")]
    [InlineData("fil")]
    [InlineData("fr")]
    [InlineData("hi")]
    [InlineData("hu")]
    [InlineData("id")]
    [InlineData("ko")]
    [InlineData("ms")]
    [InlineData("pl")]
    [InlineData("pt-BR")]
    [InlineData("ru")]
    [InlineData("th")]
    [InlineData("tr")]
    [InlineData("vi")]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    public void 変更したメッセージは各対応言語へ翻訳されている(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var resources = CreateResourceManager();

        Assert.NotEqual("Add range", resources.GetString("PriorityRectAdd", culture));
        Assert.NotEqual(
            "Used by translation modules that support context",
            resources.GetString("PriorityRectKeywordDescription", culture));
    }
}
