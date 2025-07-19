using System.Drawing;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Plugin.ColorThiefPlugin.Tests;

public class ColorThiefModuleTests
{
    private static Color FromRgb(int rgb) => Color.FromArgb(unchecked((int)0xff000000 | rgb));
    private readonly Dictionary<string, (Color expectedBack, Color expectedFront)> expectedColors = new()
    {
        { "text_000.jpg", (FromRgb(0x6c6c6c), FromRgb(0xdddddd)) }, // #6c6c6c #dddddd
        { "text_001.jpg", (FromRgb(0xacadaf), FromRgb(0x292a32)) }, // #acadaf #292a32
        { "text_002.jpg", (FromRgb(0x0e6c6c), FromRgb(0xbed0d2)) }, // #0e6c6c #bed0d2
        { "text_003.jpg", (FromRgb(0x0707b4), FromRgb(0xe1e2f2)) }, // #0707b4 #e1e2f2
        { "text_004.jpg", (FromRgb(0x10706f), FromRgb(0xcfdbdd)) }, // #10706f #cfdbdd
        { "text_005.jpg", (FromRgb(0x0808b5), FromRgb(0xc9cbf5)) }, // #0808b5 #c9cbf5
        { "text_006.jpg", (FromRgb(0xe1e2eb), FromRgb(0x0a099c)) }, // #e1e2eb #0a099c
        { "text_007.jpg", (FromRgb(0x0f6b6a), FromRgb(0xbed1d2)) }, // #0f6b6a #bed1d2
        { "text_008.jpg", (FromRgb(0xeaeaec), FromRgb(0x13137d)) }, // #eaeaec #13137d
        { "text_009.jpg", (FromRgb(0xb2b2b2), FromRgb(0x1e1e1e)) }, // #b2b2b2 #1e1e1e
        { "text_010.jpg", (FromRgb(0x156968), FromRgb(0xb7cccd)) }, // #156968 #b7cccd
        { "text_011.jpg", (FromRgb(0x0707b5), FromRgb(0xd3d4ec)) }, // #0707b5 #d3d4ec
        { "text_012.jpg", (FromRgb(0x106e6e), FromRgb(0xd6eaeb)) }, // #106e6e #d6eaeb
        { "text_013.jpg", (FromRgb(0x0908b0), FromRgb(0xd2d3eb)) }, // #0908b0 #d2d3eb
        { "text_014.jpg", (FromRgb(0x106b6b), FromRgb(0xc4d6d8)) }, // #106b6b #c4d6d8
        { "text_015.jpg", (FromRgb(0x0808b0), FromRgb(0xcccee8)) }, // #0808b0 #cccee8
        { "text_016.jpg", (FromRgb(0xaaaaaa), FromRgb(0x1e1e1e)) }, // #aaaaaa #1e1e1e
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
