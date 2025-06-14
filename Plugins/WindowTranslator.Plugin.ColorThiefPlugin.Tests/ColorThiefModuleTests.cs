using System.Drawing;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Plugin.ColorThiefPlugin.Tests;

public class ColorThiefModuleTests
{    // 各画像に対する期待値を定義（後で調整可能）
    private readonly Dictionary<string, (Color expectedBack, Color expectedFront)> expectedColors = new()
    {
        { "text_000.jpg", (Color.FromArgb(0x6c6c6c), Color.FromArgb(0xdddddd)) },
        { "text_001.jpg", (Color.FromArgb(0xacadaf), Color.FromArgb(0x292a32)) },
        { "text_002.jpg", (Color.FromArgb(0x0e6c6c), Color.FromArgb(0xbed0d2)) },
        { "text_003.jpg", (Color.FromArgb(0x0707b4), Color.FromArgb(0x0c0c79)) },
        { "text_004.jpg", (Color.FromArgb(0x10706f), Color.FromArgb(0xcfdbdd)) },
        { "text_005.jpg", (Color.FromArgb(0x0808b5), Color.FromArgb(0xc9cbf5)) },
        { "text_006.jpg", (Color.FromArgb(0xe1e2eb), Color.FromArgb(0x696898)) },
        { "text_007.jpg", (Color.FromArgb(0x0f6b6a), Color.FromArgb(0xbed1d2)) },
        { "text_008.jpg", (Color.FromArgb(0xeaeaec), Color.FromArgb(0x7c7c7c)) },
        { "text_009.jpg", (Color.FromArgb(0xb2b2b2), Color.FromArgb(0x1e1e1e)) },
        { "text_010.jpg", (Color.FromArgb(0x156968), Color.FromArgb(0xb7cccd)) },
        { "text_011.jpg", (Color.FromArgb(0x0707b5), Color.FromArgb(0x19195e)) },
        { "text_012.jpg", (Color.FromArgb(0x106e6e), Color.FromArgb(0xd6eaeb)) },
        { "text_013.jpg", (Color.FromArgb(0x0908b0), Color.FromArgb(0x0c0c4b)) },
        { "text_014.jpg", (Color.FromArgb(0x106b6b), Color.FromArgb(0xc4d6d8)) },
        { "text_015.jpg", (Color.FromArgb(0x0808b0), Color.FromArgb(0x121257)) },
        { "text_016.jpg", (Color.FromArgb(0xaaaaaa), Color.FromArgb(0x1e1e1e)) }
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
