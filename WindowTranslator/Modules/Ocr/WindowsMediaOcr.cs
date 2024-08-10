using System.ComponentModel;
using System.Text;
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
    private const double PosThrethold = .005;
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
            //.Where(w => w.Height < bitmap.PixelHeight * 0.1)
            // 少なすぎる文字も認識ミス扱い
            .Where(w => w.Text.Length > 2)
            .Where(w => !IsAllNum().IsMatch(w.Text))
            .ToArray();

        if (lineResults.IsEmpty())
        {
            return lineResults;
        }

        var pt = PosThrethold * bitmap.PixelWidth;

        var results = new List<TempMergeRect>(lineResults.Length);
        {
            var queue = new RemovableQueue<TextRect>(lineResults.OrderBy(r => r.Y));
            while (queue.TryDequeue(out var target))
            {
                var temp = new TempMergeRect(target);
                var merged = false;
                do
                {
                    merged = false;
                    foreach (var lineResult in queue.ToArray())
                    {
                        if (temp.TryMerge(lineResult, pt))
                        {
                            queue.Remove(lineResult);
                            merged = true;
                        }
                    }
                } while (merged);
                results.Add(temp);
            }
        }

        return results.Select(ToTextRect).ToArray();
    }

    private class TempMergeRect
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double FontSize { get; private set; }
        public List<TextRect> Rects { get; } = [];

        public double Left => X;
        public double Top => Y;
        public double Right => X + Width;
        public double Bottom => Y + Height;

        public TempMergeRect(TextRect rect)
        {
            (_, X, Y, Width, Height, FontSize, _, _, _, _) = rect;
            Rects.Add(rect);
        }

        public bool TryMerge(TextRect rect, double xThreshold)
        {
            //return false;
            if (!CanMerge(rect, xThreshold))
            {
                return false;
            }
            var (_, x, y, width, height, _, _, _, _, _) = rect;
            Rects.Add(rect);
            var x1 = Math.Min(X, x);
            var y1 = Math.Min(Y, y);
            var x2 = Math.Max(X + Width, x + width);
            var y2 = Math.Max(Y + Height, y + height);
            (X, Y, Width, Height) = (x1, y1, x2 - x1, y2 - y1);
            return true;
        }

        private bool CanMerge(TextRect rect, double posThreshold)
        {
            // 重なっている場合はマージできる
            if (IntersectsWith(rect))
            {
                return true;
            }

            // フォントサイズが大きく異なる場合はマージできない
            var fDiff = Math.Abs(1.0 - (FontSize / rect.FontSize)); // フォントサイズの差
            if (fDiff > FontSizeThrethold)
            {
                return false;
            }


            // x座標が近く、y間隔が近い場合にマージできる
            var (_, x, y, w, _, _, _, _, _, _) = rect;
            var xDiff = Math.Abs(X - x); // X座標の差
            var yGap = Math.Abs((Y + Height) - y); // Y座標の間隔
            var lThre = (1.0 + (fDiff / 2)) * Math.Min(FontSize, rect.FontSize) * LeadingThrethold; // 行間の閾値
            if (xDiff < posThreshold && yGap < lThre)
            {
                return true;
            }

            // y座標が近く、x間隔が近い場合にマージできる
            var xGap = Math.Min(Math.Abs((X + Width) - x), Math.Abs((x + w) - X)); // X座標の間隔
            var yDiff = Math.Abs(Y - y); // Y座標の差
            var gThre = (rect.FontSize + FontSize) * 0.5;
            if (xGap < gThre && yDiff < posThreshold)
            {
                return true;
            }
            return false;
        }

        public bool IntersectsWith(TextRect rect)
            => (rect.X < X + Width) && (X < rect.X + rect.Width) &&
            (rect.Y < Y + Height) && (Y < rect.Y + rect.Height);

        public void Deconstruct(out double x, out double y, out double width, out double height, out double fontSize, out List<TextRect> rects)
        {
            x = X;
            y = Y;
            width = Width;
            height = Height;
            fontSize = FontSize;
            rects = Rects;
        }
    }

    private TextRect ToTextRect(TempMergeRect combinedRect)
    {
        var (x, y, width, height, fontSize, rects) = combinedRect;
        var builder = new StringBuilder(combinedRect.Rects.Sum(r => r.Text.Length + 1));
        var lines = (int)(height / fontSize);
        foreach (var rect in combinedRect.Rects.OrderBy(r => (int)((r.Y - y) / fontSize)).ThenBy(r => r.X))
        {
            builder.Append(rect.Text);
            if (IsSpaceLang())
            {
                builder.Append(' ');
            }
        }
        if (IsSpaceLang())
        {
            builder.Length--;
        }

        // 若干太らせて完全に文字を覆う
        const double fat = .2;
        width += fontSize * fat;
        x -= fontSize * fat * .5;
        height += fontSize * fat;
        y -= fontSize * fat * .5;

        return new(builder.ToString(), x, y, width, height, fontSize, lines);
    }

    private bool IsSpaceLang()
        => this.source[..2] is not "ja" or "zh";

    private string CreateConcatText(string str1, string str2)
        => this.source[..2] switch
        {
            "ja" or "zh" => $"{str1}{str2}",
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
