using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WindowTranslator.ComponentModel;
using Color = System.Drawing.Color;
using ColorConverter = ColorHelper.ColorConverter;
using StudioFreesia.ColorThief;

namespace WindowTranslator.Modules.OverlayColor;

[DefaultModule]
[DisplayName("近似カラー")]
public class ColorThiefModule(ILogger<ColorThiefModule> logger) : IColorModule
{
    private readonly ILogger<ColorThiefModule> logger = logger;

    public async ValueTask<IEnumerable<TextRect>> ConvertColorAsync(SoftwareBitmap bitmap, IEnumerable<TextRect> texts)
    {
        using var ms = new InMemoryRandomAccessStream();
        var results = new List<TextRect>(texts.Count());
        var palette = TimeSpan.Zero;
        var scale = Math.Clamp(Math.Max(bitmap.PixelWidth / 1280.0, bitmap.PixelHeight / 720.0), 1, double.MaxValue);
        foreach (var text in texts)
        {
            ms.Seek(0);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, ms);
            encoder.SetSoftwareBitmap(bitmap);
            encoder.BitmapTransform.ScaledWidth = (uint)(bitmap.PixelWidth / scale);
            encoder.BitmapTransform.ScaledHeight = (uint)(bitmap.PixelHeight / scale);
            encoder.BitmapTransform.Bounds = new((uint)(text.X / scale), (uint)(text.Y / scale), (uint)(text.Width / scale), (uint)(text.Height / scale));
            await encoder.FlushAsync();

            // パレット取得が遅い
            // SIMD使えば速くなりそう…
            var now = DateTime.UtcNow;
            using var bmp = new Bitmap(ms.AsStream());
            var colors = ColorThief.GetPalette(bmp, ignoreWhite: false)
                .OrderByDescending(c => c.Population)
                .Select(c => c.Color)
                .ToArray();
            palette += DateTime.UtcNow - now;
            var back = colors[0];

            // 文字影が文字色より大きくなることがあるので、背景色とのBrightness距離が大きい方を文字色とする
            var backB = ColorConverter.RgbToHsv(new(back.R, back.G, back.B)).V;
            var front = GetDistance(backB, colors[1]) > GetDistance(backB, colors[2]) ? colors[1] : colors[2];
            results.Add(text with { Foreground = Color.FromArgb(front.R, front.G, front.B), Background = Color.FromArgb(0xF0, back.R, back.G, back.B) });
        }

        this.logger.LogDebug($"Palette:{palette}");
        return results;
    }

    private static double GetDistance(double h1, Color h2)
        => Math.Abs(h1 - ColorConverter.RgbToHsv(new(h2.R, h2.G, h2.B)).V);
}
