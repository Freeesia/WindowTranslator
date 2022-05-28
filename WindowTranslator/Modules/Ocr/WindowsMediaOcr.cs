using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace WindowTranslator.Modules.Ocr;
public class WindowsMediaOcr : IOcrModule
{
    private double IndentThrethold = .01;
    private double LeadingThrethold = .1;
    private double FontSizeThrethold = .15;

    private readonly OcrEngine ocr = OcrEngine.TryCreateFromLanguage(new("en-US"));
    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        var rawResults = await ocr.RecognizeAsync(bitmap);
        var lineResults = rawResults
            .Lines
            .Select(l => new TextRect(
                l.Text,
                l.Words.Select(w => w.BoundingRect.X).Min(),
                l.Words.Select(w => w.BoundingRect.Y).Min(),
                l.Words.Select(w => w.BoundingRect.Right).Max() - l.Words.Select(w => w.BoundingRect.Left).Min(),
                CalcFontSize(l.Text, l.Words.Select(w => w.BoundingRect.Bottom).Max() - l.Words.Select(w => w.BoundingRect.Top).Min())))
            // 大きすぎる文字は映像の認識ミスとみなす
            .Where(w => w.Height < bitmap.PixelHeight * 0.1)
            // 少なすぎる文字も認識ミス扱い
            .Where(w => w.Text.Length > 2)
            .OrderBy(w => w.X)
                .ThenBy(w => w.Y)
            .ToArray();

        if (lineResults.Length == 0)
        {
            return lineResults;
        }

        var xt = IndentThrethold * bitmap.PixelWidth;
        var yt = LeadingThrethold * bitmap.PixelHeight;

        var results = new List<TextRect>(lineResults.Length);

        var prev = lineResults.First();
        foreach (var lineResult in lineResults.Skip(1))
        {
            if (Math.Abs(prev.X - lineResult.X) < xt && Math.Abs((prev.Y + prev.Height) - lineResult.Y) < yt && Math.Abs(1.0 - (prev.FontSize / lineResult.FontSize)) < FontSizeThrethold)
            {
                prev = new(
                    prev.Y < lineResult.Y ? $"{prev.Text} {lineResult.Text}" : $"{lineResult.Text} {prev.Text}",
                    Math.Min(prev.X, lineResult.X),
                    Math.Min(prev.Y, lineResult.Y),
                    Math.Max(prev.X + prev.Width, lineResult.X + lineResult.Width) - Math.Min(prev.X, lineResult.X),
                    Math.Max(prev.Y + prev.Height, lineResult.Y + lineResult.Height) - Math.Min(prev.Y, lineResult.Y),
                    prev.FontSize + ((lineResult.FontSize - prev.FontSize) / (prev.Line + 1)),
                    prev.Line + 1);
            }
            else
            {
                results.Add(prev);
                prev = lineResult;
            }
        }
        if (results.Count == 0 || results[^1] != prev)
        {
            results.Add(prev);
        }

        return results;
    }

    private static double CalcFontSize(string text, double height)
    {
        // abcdefghijklmnopqrstuvwxyz
        // ABCDEFGHIJKLMNOPQRSTUVWXYZ
        var hasAcent = Regex.IsMatch(text, "[A-Zbdfhijklt]");
        var hasDecent = Regex.IsMatch(text, "[gjpqy]");
        return (hasAcent, hasDecent) switch
        {
            (true, true) => height,
            (false, false) => height * 1.4,
            _ => height * 1.2,
        };
    }
}
