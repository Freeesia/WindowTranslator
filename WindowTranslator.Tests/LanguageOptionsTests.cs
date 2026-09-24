namespace WindowTranslator.Tests;

public class LanguageOptionsTests
{
    [Theory]
    [InlineData("ja-JP", false)]
    [InlineData("zh-CN", false)]
    [InlineData("zh-TW", false)]
    [InlineData("en-US", true)]
    public void IsSpaceLang_ClassifiesLanguageByTwoLetterCode(string language, bool expected)
    {
        Assert.Equal(expected, LanguageUtility.IsSpaceLang(language));
    }
}
