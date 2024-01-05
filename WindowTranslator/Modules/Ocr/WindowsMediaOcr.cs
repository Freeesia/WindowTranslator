using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Modules.Ocr;

[DefaultModule]
[DisplayName("Windows標準文字認識")]
public partial class WindowsMediaOcr(IOptionsSnapshot<LanguageOptions> options) : IOcrModule
{
    private const double IndentThrethold = .005;
    private const double LeadingThrethold = .95;
    private const double FontSizeThrethold = .25;
    private readonly string source = options.Value.Source;
    private readonly OcrEngine ocr = OcrEngine.TryCreateFromLanguage(new(options.Value.Source))
            ?? throw new InvalidOperationException($"{options.Value.Source}のOCR機能が使えません。対象の言語機能をインストールしてください");

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
            .Where(w => !IsAllNum().IsMatch(w.Text))
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
                        temp.Y < lineResult.Y ? CreateConcatText(temp.Text, lineResult.Text) : CreateConcatText(lineResult.Text, temp.Text),
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

    private string CreateConcatText(string str1, string str2)
        => this.source[..2] switch
        {
            "ja" => $"{str1}{str2}",
            _ => $"{str1} {str2}",
        };


    private TextRect CalcRect(OcrLine line)
    {
        var text = this.source[..2] switch
        {
            "ja" or "zh" => string.Join(null, line.Words.Select(w => w.Text)),
            _ => line.Text,
        };
        var x = line.Words.Select(w => w.BoundingRect.X).Min();
        var y = line.Words.Select(w => w.BoundingRect.Y).Min();
        var width = line.Words.Select(w => w.BoundingRect.Right).Max() - line.Words.Select(w => w.BoundingRect.Left).Min();
        var height = line.Words.Select(w => w.BoundingRect.Bottom).Max() - line.Words.Select(w => w.BoundingRect.Top).Min();
        var fontSize = line.Words.Select(w =>
        {
            var (isxHeight, hasAcent, hasHarfAcent, hasDecent) = GetTextType(w.Text);
            return CorrectHeight(w.BoundingRect.Height, isxHeight, hasAcent, hasHarfAcent, hasDecent);
        }).Average();

        var (isxHeight, hasAcent, hasHarfAcent, hasDecent) = GetTextType(text);

        // 文字種類による位置補正
        y -= (hasAcent, hasHarfAcent) switch
        {
            (true, _) => 0,
            (false, true) => height * .1,
            (false, false) => height * .2,
        };

        // 文字種類による高さ補正
        height = CorrectHeight(height, isxHeight, hasAcent, hasHarfAcent, hasDecent);

        // 若干太らせて完全に文字を覆う
        const double fat = .2;
        width += fontSize * fat;
        x -= fontSize * fat * .5;
        height += fontSize * fat;
        y -= fontSize * fat * .5;

        return new(text, x, y, width, height, fontSize, 1);
    }

    private static double CorrectHeight(double height, bool isxHeight, bool hasAcent, bool hasHarfAcent, bool hasDecent)
        => (isxHeight, hasAcent, hasHarfAcent, hasDecent) switch
        {
            (true, true, _, true) => height,
            (true, true, _, false) => height * 1.2,
            (true, false, true, true) => height * (1 + .1 + .0),
            (true, false, false, true) => height * (1 + .2 + .0),
            (true, false, true, false) => height * (1 + .1 + .2),
            (true, false, false, false) => height * (1 + .2 + .2),
            (false, _, _, _) => height,
        };

    private static (bool isxHeight, bool hasAcent, bool hasHarfAcent, bool hasDecent) GetTextType(string text)
    {
        // abcdefghijklmnopqrstuvwxyz
        // ABCDEFGHIJKLMNOPQRSTUVWXYZ
        var isxHeight = Contains(text, "acemnosuvwxz");
        var hasAcent = Contains(text, "ABCDEFGHIJKLMNOPQRSTUVWXYZbdfhijkl");
        var hasHarfAcent = text.Contains('t');
        var hasDecent = Contains(text, "gjpqy");
        return (isxHeight, hasAcent, hasHarfAcent, hasDecent);
    }

    private static bool Contains(string text, string target)
    {
        ReadOnlySpan<char> te = text;
        ReadOnlySpan<char> ta = target;
        return te.ContainsAny(ta);
    }

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex IsAllNum();
}
