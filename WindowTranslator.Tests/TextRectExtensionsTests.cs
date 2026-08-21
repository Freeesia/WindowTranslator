using System.Drawing;

namespace WindowTranslator.Tests;

/// <summary>
/// OCR座標系の変換に関するテスト
/// </summary>
public class TextRectExtensionsTests
{
    [Theory]
    [InlineData(2.0, 10, 20, 40, 20, 10)]
    [InlineData(0.5, 40, 80, 160, 80, 40)]
    public void RestoreScaleはOCR前の画像座標へ戻す(
        double scale,
        double expectedX,
        double expectedY,
        double expectedWidth,
        double expectedHeight,
        double expectedFontSize)
    {
        var scaled = new TextRect("scaled", 20, 40, 80, 40, 20, false, Color.Black, Color.White)
        {
            Angle = 30,
            Context = "context",
        };

        var actual = scaled.RestoreScale(scale);

        Assert.Equal(expectedX, actual.X);
        Assert.Equal(expectedY, actual.Y);
        Assert.Equal(expectedWidth, actual.Width);
        Assert.Equal(expectedHeight, actual.Height);
        Assert.Equal(expectedFontSize, actual.FontSize);
        Assert.Equal(30, actual.Angle);
        Assert.Equal("context", actual.Context);
        Assert.Equal(Color.Black, actual.Foreground);
        Assert.Equal(Color.White, actual.Background);
    }
}
