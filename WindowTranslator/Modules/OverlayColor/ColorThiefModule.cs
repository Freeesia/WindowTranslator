using ColorThiefDotNet;
using System.Drawing;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Color = System.Drawing.Color;

namespace WindowTranslator.Modules.OverlayColor;

public class ColorThiefModule : IColorModule
{
    private readonly ColorThief colorThief = new();

    public async ValueTask<IEnumerable<TextRect>> ConvertColorAsync(SoftwareBitmap bitmap, IEnumerable<TextRect> texts)
    {
        using var ms = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, ms);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        var decoder = await BitmapDecoder.CreateAsync(ms);
        var results = new List<TextRect>(texts.Count());
        foreach (var text in texts)
        {
            var transform = new BitmapTransform()
            {
                Bounds = new((uint)text.X, (uint)text.Y, (uint)text.Width, (uint)text.Height),
            };
            using var crop = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Ignore, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
            using var cropStream = new InMemoryRandomAccessStream();
            var cropEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, cropStream);
            cropEncoder.SetSoftwareBitmap(crop);
            await cropEncoder.FlushAsync();
            using var bmp = new Bitmap(cropStream.AsStream());

            var colors = colorThief.GetPalette(bmp, ignoreWhite: false)
                .OrderByDescending(c => c.Population)
                .Select(c => c.Color)
                .ToArray();
            var back = colors[0];
            var front = colors[1];
            results.Add(text with { Foreground = Color.FromArgb(front.R, front.G, front.B), Background = Color.FromArgb(0xF0, back.R, back.G, back.B) });
        }
        return results;
    }
}
