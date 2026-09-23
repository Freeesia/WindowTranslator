using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

public class WindowsMediaOcrUtilityTests
{
    [Theory]
    [InlineData("日本語", true)]
    [InlineData("中文A", true)]
    [InlineData("カタカナ", true)]
    [InlineData("한글", true)]
    [InlineData("\u1100\u1161", true)]
    [InlineData("\U00020000A", true)]
    [InlineData("Latin", false)]
    [InlineData("r", false)]
    [InlineData("123", false)]
    [InlineData("", false)]
    public void Cjk文字を含む単語を判定する(string text, bool expected)
    {
        Assert.Equal(expected, WindowsMediaOcrUtility.ContainsCjk(text));
    }
}
