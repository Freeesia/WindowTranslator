using System.Globalization;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

/// <summary>
/// OCR対象範囲UIのリソースに関するテスト
/// </summary>
public class PriorityRectResourceTests
{
    [Theory]
    [InlineData("PriorityRectAdd", "Add")]
    [InlineData("PriorityRectRemove", "Remove")]
    [InlineData("PriorityRectKeyword", "Keyword")]
    [InlineData("PriorityRectKeywordDescription", "Used as context for translation")]
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
            var resources = new CustomResourceManager(
                "WindowTranslator.Properties.Resources",
                typeof(OcrTextTracker).Assembly);

            Assert.Equal(
                expected.ReplaceLineEndings("\n"),
                resources.GetString(key, CultureInfo.CurrentUICulture)?.ReplaceLineEndings("\n"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
