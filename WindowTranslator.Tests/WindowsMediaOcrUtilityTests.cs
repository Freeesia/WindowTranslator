using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

public class WindowsMediaOcrUtilityTests
{
    [Theory]
    [InlineData(0, 98, 78)]
    [InlineData(90, 272, 48)]
    [InlineData(-90, 128, 252)]
    public void 結合済み矩形を画像中心で一度だけ回転し複数行とコンテキストを維持する(double angle, double x, double y)
    {
        var source = new TextRect("paragraph", 100, 80, 80, 30, 20, true) { Context = "context" };
        var method = typeof(WindowsMediaOcr).GetMethod("ToTextRect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var result = (TextRect)method.Invoke(null, [source, angle, 200d, 150d])!;

        Assert.Equal(x, result.X, 6);
        Assert.Equal(y, result.Y, 6);
        Assert.Equal(84, result.Width, 6);
        Assert.Equal(34, result.Height, 6);
        Assert.Equal(angle, result.Angle);
        Assert.True(result.MultiLine);
        Assert.Equal("context", result.Context);
    }

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
