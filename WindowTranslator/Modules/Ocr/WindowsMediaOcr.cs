using System.Text.RegularExpressions;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using WinRT;

namespace WindowTranslator.Modules.Ocr;
public class WindowsMediaOcr : IOcrModule
{
    private double IndentThrethold = .005;
    private double LeadingThrethold = .95;
    private double FontSizeThrethold = .25;

    private readonly OcrEngine ocr = OcrEngine.TryCreateFromLanguage(new("en-US"));
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

        if (lineResults.Length == 0)
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
                    temp = new(
                        temp.Y < lineResult.Y ? $"{temp.Text} {lineResult.Text}" : $"{lineResult.Text} {temp.Text}",
                        Math.Min(temp.X, lineResult.X),
                        Math.Min(temp.Y, lineResult.Y),
                        Math.Max(temp.X + temp.Width, lineResult.X + lineResult.Width) - Math.Min(temp.X, lineResult.X),
                        Math.Max(temp.Y + temp.Height, lineResult.Y + lineResult.Height) - Math.Min(temp.Y, lineResult.Y),
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
            (false, true) => height * 0.1,
            (false, false) => height * 0.2,
        };

        // 文字種類による高さ補正
        height = (hasAcent, hasHarfAcent, hasDecent) switch
        {
            (true, _, true) => height,
            (true, _, false) => height * 1.2,
            (false, true, false) => height * (1 + 0.1 + 0.2),
            (false, false, false) => height * (1 + 0.2 + 0.2),
            (false, true, true) => height * (1 + 0.1 + 0.0),
            (false, false, true) => height * (1 + 0.2 + 0.0),
        };

        return new(text, x, y, width, height);
    }
}
