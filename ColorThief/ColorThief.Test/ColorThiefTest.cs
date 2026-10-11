using System.Drawing;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace StudioFreesia.ColorThief.Test;

public class ColorThiefTest
{
    private readonly ColorThiefDotNet.ColorThief colorThief = new();

    [Fact]
    public async Task PaletteColorsMatchColorThiefDotNetForSamePixels()
    {
        using var fileStream = new FileStream(@"images\test2.jpg", FileMode.Open, FileAccess.Read);
        var randomAccessStream = fileStream.AsRandomAccessStream();

        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var sbmp = await decoder.GetSoftwareBitmapAsync();
        using var bitmapStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, bitmapStream);
        encoder.SetSoftwareBitmap(sbmp);
        await encoder.FlushAsync();
        bitmapStream.Seek(0);
        using var bmp = new Bitmap(bitmapStream.AsStream());
        var expect = this.colorThief.GetPalette(bmp);
        var actual = ColorThief.GetPalette(sbmp).ToList();
        Assert.Equal(expect.Count, actual.Count);
        for (var i = 0; i < expect.Count; i++)
        {
            Assert.InRange(Math.Abs(expect[i].Color.R - actual[i].Color.R), 0, 1);
            Assert.InRange(Math.Abs(expect[i].Color.G - actual[i].Color.G), 0, 1);
            Assert.InRange(Math.Abs(expect[i].Color.B - actual[i].Color.B), 0, 1);
        }
    }
}
