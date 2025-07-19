using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using Microsoft.Extensions.Logging;
using StudioFreesia.ColorThief;
using Wacton.Unicolour;
using Windows.Graphics.Imaging;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;

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
        var rect = (double)Math.Abs(text.Angle) switch
        {
            < 0.001 => new Rectangle((int)text.X, (int)text.Y, (int)text.Width, (int)text.Height),
            _ => CalculateRotatedBoundingBox(text),// 回転を考慮した境界ボックスを計算
        };

        // 画像境界内にクリップ
        rect = ClipToImageBounds(rect, bitmap.PixelWidth, bitmap.PixelHeight);

        var colors = ColorThief.GetPalette(bitmap, rect, ignoreWhite: false)
            .OrderByDescending(c => c.Population)
            .Select(c => c.Color)
            .ToArray();
        var back = colors[0];

        // 文字影が文字色より大きくなることがあるので、背景色とOklch色空間の明度差が大きい方を文字色とする
        var backL = new Unicolour(ColourSpace.Rgb255, back.R, back.G, back.B).Oklch.L;
        var front = GetDistance(backL, colors[1]) > GetDistance(backL, colors[2]) ? colors[1] : colors[2];
        return (back, front);
    }

    /// <summary>
    /// 回転されたTextRectの境界ボックスを計算
    /// </summary>
    private static Rectangle CalculateRotatedBoundingBox(TextRect text)
    {
        // 回転角度をラジアンに変換
        var angleRadians = text.Angle * Math.PI / 180.0;
        var cos = (float)Math.Cos(angleRadians);
        var sin = (float)Math.Sin(angleRadians);

        // 回転の中心は左上角（text.X, text.Y）
        var centerX = (float)text.X;
        var centerY = (float)text.Y;

        // 矩形の4つの角の座標（回転中心からの相対座標）
        Span<PointF> corners =
        [
            new PointF(0.0f, 0.0f ), // 左上（回転中心）
            new PointF((float)text.Width, 0.0f ), // 右上
            new PointF((float)text.Width, (float)text.Height ), // 右下
            new PointF(0.0f, (float)text.Height),  // 左下
        ];

        for (var i = 0; i < corners.Length; i++)
        {
            var corner = corners[i];
            // 各角を回転
            corners[i] = new(corner.X * cos - corner.Y * sin + centerX, corner.X * sin + corner.Y * cos + centerY);
        }

        // 境界ボックスを計算
        var minX = Math.Min(corners[0].X, Math.Min(corners[1].X, Math.Min(corners[2].X, corners[3].X)));
        var minY = Math.Min(corners[0].Y, Math.Min(corners[1].Y, Math.Min(corners[2].Y, corners[3].Y)));
        var maxX = Math.Max(corners[0].X, Math.Max(corners[1].X, Math.Max(corners[2].X, corners[3].X)));
        var maxY = Math.Max(corners[0].Y, Math.Max(corners[1].Y, Math.Max(corners[2].Y, corners[3].Y)));

        return new Rectangle((int)Math.Floor(minX), (int)Math.Floor(minY), (int)Math.Ceiling(maxX - minX), (int)Math.Ceiling(maxY - minY));
    }

    /// <summary>
    /// 矩形を画像境界内にクリップ
    /// </summary>
    private static Rectangle ClipToImageBounds(Rectangle rect, int imageWidth, int imageHeight)
    {
        var x = Math.Max(0, rect.X);
        var y = Math.Max(0, rect.Y);
        var right = Math.Min(imageWidth, rect.X + rect.Width);
        var bottom = Math.Min(imageHeight, rect.Y + rect.Height);

        return new Rectangle(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    private static double GetDistance(double h1, Color h2)
        => Math.Abs(h1 - new Unicolour(ColourSpace.Rgb255, h2.R, h2.G, h2.B).Oklch.L);
}
