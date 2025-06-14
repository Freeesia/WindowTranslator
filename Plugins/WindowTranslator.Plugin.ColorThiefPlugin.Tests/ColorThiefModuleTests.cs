using System.Drawing;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Plugin.ColorThiefPlugin.Tests;

public class ColorThiefModuleTests
{
    // 各画像に対する期待値を定義（後で調整可能）
    private readonly Dictionary<string, (Color expectedBack, Color expectedFront)> expectedColors = new()
    {
        { "text_000.jpg", (Color.FromArgb(255, 240, 240, 240), Color.FromArgb(255, 50, 50, 50)) },
        { "text_001.jpg", (Color.FromArgb(255, 230, 230, 230), Color.FromArgb(255, 40, 40, 40)) },
        { "text_002.jpg", (Color.FromArgb(255, 220, 220, 220), Color.FromArgb(255, 60, 60, 60)) },
        { "text_003.jpg", (Color.FromArgb(255, 250, 250, 250), Color.FromArgb(255, 30, 30, 30)) },
        { "text_004.jpg", (Color.FromArgb(255, 200, 200, 200), Color.FromArgb(255, 80, 80, 80)) },
        { "text_005.jpg", (Color.FromArgb(255, 210, 210, 210), Color.FromArgb(255, 70, 70, 70)) },
        { "text_006.jpg", (Color.FromArgb(255, 190, 190, 190), Color.FromArgb(255, 90, 90, 90)) },
        { "text_007.jpg", (Color.FromArgb(255, 180, 180, 180), Color.FromArgb(255, 100, 100, 100)) },
        { "text_008.jpg", (Color.FromArgb(255, 170, 170, 170), Color.FromArgb(255, 110, 110, 110)) },
        { "text_009.jpg", (Color.FromArgb(255, 160, 160, 160), Color.FromArgb(255, 120, 120, 120)) },
        { "text_010.jpg", (Color.FromArgb(255, 150, 150, 150), Color.FromArgb(255, 130, 130, 130)) },
        { "text_011.jpg", (Color.FromArgb(255, 140, 140, 140), Color.FromArgb(255, 140, 140, 140)) },
        { "text_012.jpg", (Color.FromArgb(255, 130, 130, 130), Color.FromArgb(255, 150, 150, 150)) },
        { "text_013.jpg", (Color.FromArgb(255, 120, 120, 120), Color.FromArgb(255, 160, 160, 160)) },
        { "text_014.jpg", (Color.FromArgb(255, 110, 110, 110), Color.FromArgb(255, 170, 170, 170)) },
        { "text_015.jpg", (Color.FromArgb(255, 100, 100, 100), Color.FromArgb(255, 180, 180, 180)) },
        { "text_016.jpg", (Color.FromArgb(255, 90, 90, 90), Color.FromArgb(255, 190, 190, 190)) }
    };

    [Theory]
    [InlineData("text_000.jpg")]
    [InlineData("text_001.jpg")]
    [InlineData("text_002.jpg")]
    [InlineData("text_003.jpg")]
    [InlineData("text_004.jpg")]
    [InlineData("text_005.jpg")]
    [InlineData("text_006.jpg")]
    [InlineData("text_007.jpg")]
    [InlineData("text_008.jpg")]
    [InlineData("text_009.jpg")]
    [InlineData("text_010.jpg")]
    [InlineData("text_011.jpg")]
    [InlineData("text_012.jpg")]
    [InlineData("text_013.jpg")]
    [InlineData("text_014.jpg")]
    [InlineData("text_015.jpg")]
    [InlineData("text_016.jpg")]
    public async Task DetectColors_ShouldReturnExpectedBackgroundAndForegroundColors(string imageName)
    {
        // Arrange
        var imagePath = Path.Combine("images", imageName);
        Assert.True(File.Exists(imagePath), $"Test image {imagePath} not found");

        using var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
        using var randomAccessStream = fileStream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();

        // テキスト矩形を全体画像として設定
        var textRect = new TextRect(
            "sample text",
            0, 0,
            bitmap.PixelWidth, bitmap.PixelHeight,
            12, false,
            Color.Black, Color.White);

        var (expectedBack, expectedFront) = expectedColors[imageName];

        // Act
        var (actualBack, actualFront) = ColorThiefModule.DetectColors(bitmap, textRect);

        // Assert
        Assert.Equal(expectedBack, actualBack);
        Assert.Equal(expectedFront, actualFront);
    }
}
