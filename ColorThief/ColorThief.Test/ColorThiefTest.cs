using System.Drawing;
using Windows.Graphics.Imaging;

namespace StudioFreesia.ColorThief.Test;

public class ColorThiefTest
{
    private readonly ColorThiefDotNet.ColorThief colorThief = new();

    [Fact]
    public async Task Test1()
    {
        using var fileStream = new FileStream(@"images\test2.jpg", FileMode.Open, FileAccess.Read);
        var randomAccessStream = fileStream.AsRandomAccessStream();

        var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        using var sbmp = await decoder.GetSoftwareBitmapAsync();
        using var bmp = new Bitmap(randomAccessStream.AsStream());
        var expect = this.colorThief.GetPalette(bmp);
        var actual = ColorThief.GetPalette(sbmp, quality: 10000);
        Assert.Equal(expect.Count, actual.Count);
        for (var i = 0; i < expect.Count; i++)
        {
            Assert.Equal(expect[i].Color.R, actual[i].Color.R);
            Assert.Equal(expect[i].Color.G, actual[i].Color.G);
            Assert.Equal(expect[i].Color.B, actual[i].Color.B);
            Assert.Equal(expect[i].Population, actual[i].Population);
        }
    }
}