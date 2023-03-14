using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace WindowTranslator.Modules.Ocr;
public class WindowsMediaOcr : IOcrModule
{
    private const double IndentThrethold = .005;
    private const double LeadingThrethold = .95;
    private const double FontSizeThrethold = .25;

    private readonly OcrEngine ocr;

    public WindowsMediaOcr(IOptionsSnapshot<LanguageOptions> options)
    {
        this.ocr = OcrEngine.TryCreateFromLanguage(new(options.Value.Source))
            ?? throw new InvalidOperationException($"{options.Value.Source}のOCR機能が使えなかった");
    }

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        var rawResults = await ocr.RecognizeAsync(bitmap);
        var lineResults = rawResults
            .Lines
            .Select(CalcRect)
            // 大きすぎる文字は映像の認識ミスとみなす
            .Where(w => w.Height < bitmap.PixelHeight * 0.1)
            // 少なすぎる文字も認識ミス扱い
            .Where(w => w.Text.Length > 2)
            .OrderBy(w => w.Y)
                .ThenBy(w => w.X)
            .ToArray();

        if (lineResults.IsEmpty())
        {
            return lineResults;
        }

        var xt = IndentThrethold * bitmap.PixelWidth;

        var results = new List<TextRect>(lineResults.Length);
        var queue = new RemovableQueue<TextRect>(lineResults);
        while (queue.TryDequeue(out var target))
        {
            var temp = target;
            foreach (var lineResult in queue.ToArray())
            {
                var xDiff = Math.Abs(temp.X - lineResult.X);
                var yDiff = Math.Abs((temp.Y + temp.Height) - lineResult.Y);
                var fDiff = Math.Abs(1.0 - (temp.FontSize / lineResult.FontSize));
                var lThre = (1.0 + (fDiff / 2)) * Math.Min(temp.FontSize, lineResult.FontSize) * LeadingThrethold;
                if (xDiff < xt && yDiff < lThre && fDiff < FontSizeThrethold)
                {
                    var top = Math.Min(temp.Y, lineResult.Y);
                    var bottom = Math.Max(temp.Y + temp.Height, lineResult.Y + lineResult.Height);
                    var left = Math.Min(temp.X, lineResult.X);
                    var right = Math.Max(temp.X + temp.Width, lineResult.X + lineResult.Width);
                    temp = new(
                        temp.Y < lineResult.Y ? $"{temp.Text} {lineResult.Text}" : $"{lineResult.Text} {temp.Text}",
                        left,
                        top,
                        right - left,
                        bottom - top,
                        temp.FontSize + ((lineResult.FontSize - temp.FontSize) / (temp.Line + 1)),
                        temp.Line + 1);
                    queue.Remove(lineResult);
                }
            }
            results.Add(temp);
        }

        return results;
    }

    private static TextRect CalcRect(OcrLine line)
    {
        var text = line.Text;
        var x = line.Words.Select(w => w.BoundingRect.X).Min();
        var y = line.Words.Select(w => w.BoundingRect.Y).Min();
        var width = line.Words.Select(w => w.BoundingRect.Right).Max() - line.Words.Select(w => w.BoundingRect.Left).Min();
        var height = line.Words.Select(w => w.BoundingRect.Bottom).Max() - line.Words.Select(w => w.BoundingRect.Top).Min();

        // abcdefghijklmnopqrstuvwxyz
        // ABCDEFGHIJKLMNOPQRSTUVWXYZ
        var hasAcent = Regex.IsMatch(text, "[A-Zbdfhijkl]");
        var hasHarfAcent = text.Contains('t');
        var hasDecent = Regex.IsMatch(text, "[gjpqy]");

        // 文字種類による位置補正
        y -= (hasAcent, hasHarfAcent) switch
        {
            (true, _) => 0,
            (false, true) => height * .1,
            (false, false) => height * .2,
        };

        // 文字種類による高さ補正
        height = (hasAcent, hasHarfAcent, hasDecent) switch
        {
            (true, _, true) => height,
            (true, _, false) => height * 1.2,
            (false, true, true) => height * (1 + .1 + .0),
            (false, false, true) => height * (1 + .2 + .0),
            (false, true, false) => height * (1 + .1 + .2),
            (false, false, false) => height * (1 + .2 + .2),
        };

        var fontSize = height;

        // 若干太らせて完全に文字を覆う
        const double fat = .2;
        width += fontSize * fat;
        x -= fontSize * fat * .5;
        height += fontSize * fat;
        y -= fontSize * fat * .5;

        return new(text, x, y, width, height, fontSize, 1);
    }
}
