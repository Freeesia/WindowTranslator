using Microsoft.Extensions.Logging;
using System.ComponentModel;
using Windows.Graphics.Imaging;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using ColorConverter = ColorHelper.ColorConverter;
using StudioFreesia.ColorThief;
using System.Drawing;
using System.Diagnostics;

namespace WindowTranslator.Plugin.ColorThiefPlugin;

[DefaultModule]
[DisplayName("近似カラー")]
public class ColorThiefModule(ILogger<ColorThiefModule> logger) : IColorModule
{
    private readonly ILogger<ColorThiefModule> logger = logger;

    public ValueTask<IEnumerable<TextRect>> ConvertColorAsync(SoftwareBitmap bitmap, IEnumerable<TextRect> texts)
    {
        var results = new List<TextRect>(texts.Count());
        var sw = Stopwatch.StartNew();
        // テキスト数が少ない時に並列化すると逆に遅くなり、総合すると並列化しないほうがよさそう
        foreach (var text in texts)
        {
            var (back, front) = DetectColors(bitmap, text);
            results.Add(text with { Foreground = Color.FromArgb(front.R, front.G, front.B), Background = Color.FromArgb(0xF0, back.R, back.G, back.B) });
        }

        this.logger.LogDebug($"Palette:{sw.Elapsed}");
        return new(results);
    }

    public static (Color back, Color front) DetectColors(SoftwareBitmap bitmap, TextRect text)
    {
        var colors = ColorThief.GetPalette(bitmap, new Rectangle((int)text.X, (int)text.Y, (int)text.Width, (int)text.Height), ignoreWhite: false)
            .OrderByDescending(c => c.Population)
            .Select(c => c.Color)
            .ToArray();
        var back = colors[0];

        // 文字影が文字色より大きくなることがあるので、背景色とのBrightness距離が大きい方を文字色とする
        var backB = ColorConverter.RgbToHsv(new(back.R, back.G, back.B)).V;
        var front = GetDistance(backB, colors[1]) > GetDistance(backB, colors[2]) ? colors[1] : colors[2];
        return (back, front);
    }

    private static double GetDistance(double h1, Color h2)
        => Math.Abs(h1 - ColorConverter.RgbToHsv(new(h2.R, h2.G, h2.B)).V);
}
