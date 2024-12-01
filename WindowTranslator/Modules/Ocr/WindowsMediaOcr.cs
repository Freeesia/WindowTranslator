using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using WindowTranslator.ComponentModel;
using WindowTranslator.Extensions;
using static WindowTranslator.Modules.Ocr.WindowsMediaOcrUtility;

namespace WindowTranslator.Modules.Ocr;

[DefaultModule]
[DisplayName("Windows標準文字認識")]
public partial class WindowsMediaOcr(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<WindowsMediaOcrParam> ocrParam, ILogger<WindowsMediaOcr> logger) : IOcrModule
{
    private readonly double xPosThrethold = ocrParam.Value.XPosThrethold;
    private readonly double yPosThrethold = ocrParam.Value.YPosThrethold;
    private readonly double leadingThrethold = ocrParam.Value.LeadingThrethold;
    private readonly double spacingThreshold = ocrParam.Value.SpacingThreshold;
    private readonly double fontSizeThrethold = ocrParam.Value.FontSizeThrethold;
    private readonly bool isAvoidMergeList = ocrParam.Value.IsAvoidMergeList;
    private readonly string source = langOptions.Value.Source;
    private readonly OcrEngine ocr = OcrEngine.TryCreateFromLanguage(new(ConvertLanguage(langOptions.Value.Source)))
            ?? throw new InvalidOperationException($"{langOptions.Value.Source}のOCR機能が使えません。対象の言語機能をインストールしてください");
    private readonly ILogger<WindowsMediaOcr> logger = logger;

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        // 認識前に縮小したら時間短くなるかと思ったけど、速くはなるけど精度が落ちるのでやめた
        var t = this.logger.LogDebugTime("OCR Recognize");
        var rawResults = await ocr.RecognizeAsync(bitmap);
        t.Dispose();

        // フィルター＆マージ処理について
        // 1. 認識直後にワード単位のフィルター
        //     * おかしい先頭文字(@,O,Ö)
        //     * 2文字以上かつ同じ文字で構成されている
        // 2. ワードからブロックへマージ
        //     * 文字種類による位置・サイズ補正
        // 3. ブロック単位のフィルター
        //     * 中途半端な文字列での判定なので、極力ここの処理は減らす
        //     * 大きすぎる文字は映像の認識ミスとみなす
        // 4. ブロック同士のマージ
        //     * 座標が近いor被ってる場合にマージできる
        // 5. マージ後のフィルター
        //     * 最終的な文字列で判定する
        //     * 少なすぎる文字も認識ミス扱い
        //     * 全部数字なら対象外


        var lineResults = rawResults
            .Lines
            .Select(CalcRect)
            // 大きすぎる文字は映像の認識ミスとみなす
            .Where(w => w.Height < bitmap.PixelHeight * 0.1)
            .ToArray();

        if (lineResults.IsEmpty())
        {
            return lineResults;
        }

        var xt = xPosThrethold * bitmap.PixelWidth;
        var yt = yPosThrethold * bitmap.PixelHeight;

        var results = new List<TempMergeRect>(lineResults.Length);
        {
            var queue = new RemovableQueue<TextRect>(lineResults.OrderBy(r => r.Y));
            while (queue.TryDequeue(out var target))
            {
                var temp = new TempMergeRect(this.source, target);
                var merged = false;
                do
                {
                    merged = false;
                    foreach (var lineResult in queue.ToArray())
                    {
                        if (CanMerge(temp, lineResult, xt, yt))
                        {
                            temp.Merge(lineResult);
                            queue.Remove(lineResult);
                            merged = true;
                        }
                    }
                } while (merged);
                results.Add(temp);
            }
        }

        return results.Select(ToTextRect)
            // マージ後に少なすぎる文字も認識ミス扱い
            .Where(w => w.Text.Length > 2)
            // 全部数字なら対象外
            .Where(w => !IsAllSymbolOrSpace().IsMatch(w.Text))
            .ToArray();
    }

    private bool CanMerge(TempMergeRect temp, TextRect rect, double xThreshold, double yThreshold)
    {
        // 重なっている場合はマージできる
        if (temp.IntersectsWith(rect))
        {
            return true;
        }

        // フォントサイズが大きく異なる場合はマージできない
        var fDiff = Math.Abs(temp.FontSize - rect.FontSize) / Math.Min(temp.FontSize, rect.FontSize);
        if (fDiff > fontSizeThrethold)
        {
            return false;
        }

        var fontSize = temp.Rects.Append(rect).Average(r => r.FontSize);
        var (text, x, y, w, _, _, _, _, _, _) = rect;

        // y座標が近く、x間隔が近い場合にマージできる
        var xGap = Math.Min(Math.Abs((temp.X + temp.Width) - x), Math.Abs((x + w) - temp.X)); // X座標の間隔
        var yDiff = Math.Abs(temp.Y - y); // Y座標の差
        var gThre = fontSize * spacingThreshold;
        if (xGap < gThre && yDiff < yThreshold)
        {
            return true;
        }

        // マージ元先の文字列が両方2単語未満の場合は縦にマージできない
        if (this.isAvoidMergeList && (IsSpaceLang(this.source) ? (WordCount(temp.Text) <= 2 && WordCount(text) <= 2) : (temp.Text.Length <= 8 && text.Length <= 8)))
        {
            return false;
        }

        // x座標が近く、y間隔が近い場合にマージできる
        var xDiff = Math.Abs(temp.X - x); // X座標の差
        var yGap = Math.Abs((temp.Y + temp.Height) - y); // Y座標の間隔
        var lThre = fontSize * leadingThrethold; // 行間の閾値
        if (xDiff < xThreshold && yGap < lThre)
        {
            return true;
        }

        // x座標の中心が近く、y間隔が近い場合にマージできる
        var xCenter2 = x + (w * .5);
        var xCenterDiff = Math.Abs(temp.CenterX - xCenter2); // X座標の中心の差
        if (xCenterDiff < xThreshold && yGap < lThre)
        {
            return true;
        }
        return false;
    }

    private class TempMergeRect
    {
        private readonly string lang;
        private readonly ResetableLazy<string> text;
        private readonly List<TextRect> rects;

        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double FontSize { get; private set; }

        public double Left => X;
        public double Top => Y;
        public double Right => X + Width;
        public double Bottom => Y + Height;
        public double CenterX => X + (Width * .5);
        public IReadOnlyList<TextRect> Rects => this.rects;
        public string Text => text.Value;

        public TempMergeRect(string lang, TextRect rect)
        {
            this.lang = lang;
            this.text = new(CreateText, LazyThreadSafetyMode.None);
            (_, X, Y, Width, Height, FontSize, _, _, _, _) = rect;
            this.rects = [rect];
        }

        public void Merge(TextRect rect)
        {
            var (_, x, y, width, height, _, _, _, _, _) = rect;
            this.rects.Add(rect);
            var x1 = Math.Min(X, x);
            var y1 = Math.Min(Y, y);
            var x2 = Math.Max(X + Width, x + width);
            var y2 = Math.Max(Y + Height, y + height);
            (X, Y, Width, Height) = (x1, y1, x2 - x1, y2 - y1);
            FontSize = Rects.Average(r => r.FontSize);
            this.text.Reset();
        }

        public bool IntersectsWith(TextRect rect)
            => (rect.X < X + Width) && (X < rect.X + rect.Width) && (rect.Y < Y + Height) && (Y < rect.Y + rect.Height);

        private string CreateText()
        {
            var builder = new StringBuilder(this.Rects.Sum(r => r.Text.Length + 1));
            foreach (var rect in this.Rects.OrderBy(r => (int)((r.Y - this.Y) / this.FontSize)).ThenBy(r => r.X))
            {
                builder.Append(rect.Text);
                if (IsSpaceLang(this.lang))
                {
                    builder.Append(' ');
                }
            }
            if (IsSpaceLang(this.lang))
            {
                builder.Length--;
            }
            return builder.ToString();
        }

        public void Deconstruct(out double x, out double y, out double width, out double height, out double fontSize, out IReadOnlyList<TextRect> rects)
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
        var (x, y, width, height, fontSize, _) = combinedRect;
        var text = combinedRect.Text;
        // 高さがフォントサイズの2倍以上の場合は複数行とみなす
        // または、
        // スペース言語の場合は単語数が2以上、それ以外の場合は文字数が8文字以上の場合は複数行とみなす(やっぱり微妙…)
        var lines = height / fontSize >= 2;
        //|| (IsSpaceLang(this.source) ? (WordCount(text) > 2) : (text.Length > 8));

        // 若干太らせて完全に文字を覆う
        const double fat = .2;
        width += fontSize * fat;
        x -= fontSize * fat * .5;
        height += fontSize * fat;
        y -= fontSize * fat * 1.5;

        return new(text, x, y, width, height, fontSize, lines);
    }

    private static bool IsSpaceLang(string lang)
        => lang[..2] is not "ja" or "zh";

    private static int WordCount(string text)
    {
        var span = text.AsSpan();
        var count = 0;
        while (!span.IsEmpty)
        {
            count++;
            var index = span.IndexOf(' ');
            if (index == -1)
            {
                break;
            }
            span = span[(index + 1)..];
        }
        return count;
    }

    private TextRect CalcRect(OcrLine line)
    {
        if (IsIgnoreLine().IsMatch(line.Text))
        {
            return TextRect.Empty;
        }
        // ワードのフィルタリング
        var words = FilterWords(line.Words).ToArray();
        if (words.Length == 0)
        {
            return TextRect.Empty;
        }
        var text = IsSpaceLang(this.source)
            ? string.Join(" ", words.Select(w => w.Text))
            : string.Concat(words.Select(w => w.Text));
        var x = words.Select(w => w.BoundingRect.X).Min();
        var y = words.Select(w => w.BoundingRect.Y).Min();
        var width = words.Select(w => w.BoundingRect.Right).Max() - words.Select(w => w.BoundingRect.Left).Min();
        var height = words.Select(w => w.BoundingRect.Bottom).Max() - words.Select(w => w.BoundingRect.Top).Min();
        var fontSize = words.Select(w =>
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

        return new(text, x, y, width, height, fontSize, false);
    }

    private static IEnumerable<OcrWord> FilterWords(IEnumerable<OcrWord> words)
    {
        // 先頭が@やOの場合は何かしらのアイコンの可能性が高いので無視
        while (words.FirstOrDefault()?.Text is "@" or "O" or "Ö" or "Ü")
        {
            words = words.Skip(1);
        }
        // 3文字以上かつ同じ文字で構成されている場合は無視
        // `•`は大抵の場合は認識ミスなので無視
        words = words.Where(w => w.Text.Length < 3 || !IsAllSameChar(w.Text.Replace("•", string.Empty)));
        return words;
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

    [GeneratedRegex(@"^[\s\p{S}\p{P}\d]+$")]
    private static partial Regex IsAllSymbolOrSpace();

    /// <summary>
    /// 認識ミスとして無視する文字列
    /// * 4文字以上aoeのみで構成されているかどうか
    /// </summary>
    [GeneratedRegex(@"^[aceo@]{3,}$")]
    private static partial Regex IsIgnoreLine();

    private static bool IsAllSameChar(string text)
    {
        ReadOnlySpan<char> chars = text;
        return !chars[1..].ContainsAnyExcept(chars[0]);
    }
}
