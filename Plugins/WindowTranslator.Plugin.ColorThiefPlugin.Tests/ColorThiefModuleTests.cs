using System.Drawing;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Plugin.ColorThiefPlugin.Tests;

public class ColorThiefModuleTests
{    // 各画像に対する期待値を定義（後で調整可能）
    private readonly Dictionary<string, (Color expectedBack, Color expectedFront)> expectedColors = new()
    {
        { "text_000.jpg", (Color.FromArgb(0xF0, 0xF0, 0xF0), Color.FromArgb(0x32, 0x32, 0x32)) },
        { "text_001.jpg", (Color.FromArgb(0xE6, 0xE6, 0xE6), Color.FromArgb(0x28, 0x28, 0x28)) },
        { "text_002.jpg", (Color.FromArgb(0xDC, 0xDC, 0xDC), Color.FromArgb(0x3C, 0x3C, 0x3C)) },
        { "text_003.jpg", (Color.FromArgb(0xFA, 0xFA, 0xFA), Color.FromArgb(0x1E, 0x1E, 0x1E)) },
        { "text_004.jpg", (Color.FromArgb(0xC8, 0xC8, 0xC8), Color.FromArgb(0x50, 0x50, 0x50)) },
        { "text_005.jpg", (Color.FromArgb(0xD2, 0xD2, 0xD2), Color.FromArgb(0x46, 0x46, 0x46)) },
        { "text_006.jpg", (Color.FromArgb(0xBE, 0xBE, 0xBE), Color.FromArgb(0x5A, 0x5A, 0x5A)) },
        { "text_007.jpg", (Color.FromArgb(0xB4, 0xB4, 0xB4), Color.FromArgb(0x64, 0x64, 0x64)) },
        { "text_008.jpg", (Color.FromArgb(0xAA, 0xAA, 0xAA), Color.FromArgb(0x6E, 0x6E, 0x6E)) },
        { "text_009.jpg", (Color.FromArgb(0xA0, 0xA0, 0xA0), Color.FromArgb(0x78, 0x78, 0x78)) },
        { "text_010.jpg", (Color.FromArgb(0x96, 0x96, 0x96), Color.FromArgb(0x82, 0x82, 0x82)) },
        { "text_011.jpg", (Color.FromArgb(0x8C, 0x8C, 0x8C), Color.FromArgb(0x8C, 0x8C, 0x8C)) },
        { "text_012.jpg", (Color.FromArgb(0x82, 0x82, 0x82), Color.FromArgb(0x96, 0x96, 0x96)) },
        { "text_013.jpg", (Color.FromArgb(0x78, 0x78, 0x78), Color.FromArgb(0xA0, 0xA0, 0xA0)) },
        { "text_014.jpg", (Color.FromArgb(0x6E, 0x6E, 0x6E), Color.FromArgb(0xAA, 0xAA, 0xAA)) },
        { "text_015.jpg", (Color.FromArgb(0x64, 0x64, 0x64), Color.FromArgb(0xB4, 0xB4, 0xB4)) },
        { "text_016.jpg", (Color.FromArgb(0x5A, 0x5A, 0x5A), Color.FromArgb(0xBE, 0xBE, 0xBE)) }
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
