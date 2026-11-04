extern alias OneOcr;

using OneOcrUtility = OneOcr::WindowTranslator.Plugin.OneOcrPlugin.Utility;

namespace WindowTranslator.Tests;

public class OneOcrMarginTests
{
    [Fact]
    public void 一行の矩形は行の高さの4パーセントを上下に付ける()
    {
        var rect = new TextRect("single line", 100, 200, 300, 30, 27, false);

        var result = OneOcrUtility.AddMargin(rect, 5);

        Assert.Equal(95, result.X, 6);
        Assert.Equal(310, result.Width, 6);
        Assert.Equal(198.8, result.Y, 6);
        Assert.Equal(32.4, result.Height, 6);
    }

    [Fact]
    public void 長い段落でも縦の余白は一行分の基準から増えない()
    {
        // 13行の段落を想定（行の高さ30、段落の高さ451）
        var paragraph = new TextRect("long paragraph", 100, 200, 300, 451, 27, true);
        var line = new TextRect("single line", 100, 200, 300, 30, 27, false);

        var paragraphResult = OneOcrUtility.AddMargin(paragraph, 5);
        var lineResult = OneOcrUtility.AddMargin(line, 5);

        Assert.Equal(lineResult.Height - line.Height, paragraphResult.Height - paragraph.Height, 6);
        Assert.Equal(lineResult.Y, paragraphResult.Y, 6);
    }

    [Fact]
    public void 文字サイズが取得できない場合は矩形の高さを基準にする()
    {
        var rect = new TextRect("unknown size", 0, 50, 100, 20, 0, false);

        var result = OneOcrUtility.AddMargin(rect, 0);

        Assert.Equal(49.2, result.Y, 6);
        Assert.Equal(21.6, result.Height, 6);
    }
}
