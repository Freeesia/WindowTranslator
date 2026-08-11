namespace WindowTranslator.Tests;

/// <summary>
/// OCRの座標系変換に関するテスト
/// </summary>
public class OcrUtilityTests
{
    [Theory]
    [InlineData(0.005, 1920, 2.0, 19.2)]
    [InlineData(0.005, 1920, 0.5, 4.8)]
    [InlineData(0.010, 800, 1.0, 8.0)]
    public void 相対閾値をスケール後画像の座標系へ変換する(
        double relativeThreshold,
        int sourcePixels,
        double scale,
        double expected)
    {
        var actual = OcrUtility.ToScaledThreshold(relativeThreshold, sourcePixels, scale);

        Assert.Equal(expected, actual, 10);
    }
}
