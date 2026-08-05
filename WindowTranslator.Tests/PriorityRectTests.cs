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
    public void Clampは画像の範囲外にはみ出した矩形を切り詰める()
    {
        var rect = new RectInfo(-10, -20, 100, 100);

        var clamped = rect.Clamp(50, 50);

        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
        Assert.Equal(50, clamped.Width);
        Assert.Equal(50, clamped.Height);
    }

    [Fact]
    public void Clampは画像の外にある矩形を空にする()
    {
        var rect = new RectInfo(100, 100, 50, 50);

        var clamped = rect.Clamp(50, 50);

        Assert.True(clamped.IsEmpty);
    }

    [Theory]
    // 完全に含まれる場合は1.0
    [InlineData(20, 20, 10, 10, 1.0)]
    // 面積の4分の1だけ重なる場合は0.25
    [InlineData(5, 5, 10, 10, 0.25)]
    // 重なっていない場合は0.0
    [InlineData(100, 100, 10, 10, 0.0)]
    public void IntersectionRatioは対象の面積に対する重なりの割合を返す(double x, double y, double width, double height, double expected)
    {
        var area = new RectInfo(10, 10, 50, 50);

        var ratio = area.IntersectionRatio(new(x, y, width, height));

        Assert.Equal(expected, ratio, 5);
    }

    [Fact]
    public void IntersectionRatioは面積が0の矩形に対して0を返す()
    {
        var area = new RectInfo(0, 0, 50, 50);

        Assert.Equal(0, area.IntersectionRatio(new(10, 10, 0, 10)));
    }
}
