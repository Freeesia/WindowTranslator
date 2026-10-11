namespace WindowTranslator.Tests;

/// <summary>
/// 優先矩形の座標計算に関するテスト
/// </summary>
public class PriorityRectTests
{
    [Fact]
    public void ToAbsoluteRectは画像サイズに応じた絶対座標を返す()
    {
        var rect = new PriorityRect(0.25, 0.5, 0.25, 0.25);

        var abs = rect.ToAbsoluteRect(800, 600);

        Assert.Equal(200, abs.X);
        Assert.Equal(300, abs.Y);
        Assert.Equal(200, abs.Width);
        Assert.Equal(150, abs.Height);
    }

    [Fact]
    public void FromAbsoluteRectは相対座標に変換する()
    {
        var rect = PriorityRect.FromAbsoluteRect(200, 300, 200, 150, 800, 600, "keyword");

        Assert.Equal(0.25, rect.X);
        Assert.Equal(0.5, rect.Y);
        Assert.Equal(0.25, rect.Width);
        Assert.Equal(0.25, rect.Height);
        Assert.Equal("keyword", rect.Keyword);
    }

    [Fact]
    public void FromAbsoluteRectは小数の基準サイズを保持して相対座標に変換する()
    {
        var rect = PriorityRect.FromAbsoluteRect(400.4, 300.4, 200.2, 150.2, 800.8, 600.8);

        Assert.Equal(0.5, rect.X);
        Assert.Equal(0.5, rect.Y);
        Assert.Equal(0.25, rect.Width);
        Assert.Equal(0.25, rect.Height);
    }

}
