using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WindowTranslator.ComponentModel;
using WindowTranslator.Extensions;
using static WindowTranslator.Modules.Ocr.Utility;
using static WindowTranslator.Modules.Ocr.WindowsMediaOcrUtility;

namespace WindowTranslator.Modules.Ocr;

[DefaultModule]
[DisplayName("Windows標準文字認識")]
public sealed partial class WindowsMediaOcr(
    IOptionsSnapshot<LanguageOptions> langOptions,
    IOptionsSnapshot<WindowsMediaOcrParam> ocrParam,
    ILogger<WindowsMediaOcr> logger)
    : IOcrModule, IDisposable
{
    private readonly double xPosThrethold = ocrParam.Value.XPosThrethold;
    private readonly double yPosThrethold = ocrParam.Value.YPosThrethold;
    private readonly double leadingThrethold = ocrParam.Value.LeadingThrethold;
    private readonly double spacingThreshold = ocrParam.Value.SpacingThreshold;
    private readonly double fontSizeThrethold = ocrParam.Value.FontSizeThrethold;
    private readonly bool isAvoidMergeList = ocrParam.Value.IsAvoidMergeList;
    private readonly string source = langOptions.Value.Source;
    private readonly double scale = ocrParam.Value.Scale;
    private readonly OcrEngine ocr = OcrEngine.TryCreateFromLanguage(new(ConvertLanguage(langOptions.Value.Source)))
            ?? throw new InvalidOperationException($"{langOptions.Value.Source}のOCR機能が使えません。対象の言語機能をインストールしてください");
    private readonly ILogger<WindowsMediaOcr> logger = logger;
    private readonly InMemoryRandomAccessStream resizeStream = new();

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        // リサイズが必要かどうか
        // 画像の幅または高さがリサイズ後の幅または高さを超える場合はリサイズが必要
        var needScale = ((int)(this.scale * bitmap.PixelWidth) > bitmap.PixelWidth)
            || ((int)(this.scale * bitmap.PixelHeight) > bitmap.PixelHeight);

        var newWidth = (uint)(bitmap.PixelWidth * scale);
        var newHeight = (uint)(bitmap.PixelHeight * scale);
        if (newWidth > OcrEngine.MaxImageDimension || newHeight > OcrEngine.MaxImageDimension)
        {
            throw new InvalidOperationException("ウィンドウサイズが大きすぎます。対象ウィンドウのサイズを小さくするか、認識設定の拡大率を下げてください。");
        }

        // 拡大率に基づくリサイズ処理
        var workingBitmap = needScale ? await ResizeSoftwareBitmapAsync(bitmap, this.scale) : bitmap;

        var t = this.logger.LogDebugTime("OCR Recognize");
        var rawResults = await ocr.RecognizeAsync(workingBitmap);
        t.Dispose();

        // 角度と中心座標を取得
        var angle = rawResults.TextAngle ?? 0;
        var centerX = workingBitmap.PixelWidth / 2.0;
        var centerY = workingBitmap.PixelHeight / 2.0;

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
            .Select(line => CalcRect(line, angle, centerX, centerY))
            // 大きすぎる文字は映像の認識ミスとみなす
            .Where(w => w.Height < workingBitmap.PixelHeight * 0.1)
            .ToArray();

        if (lineResults.IsEmpty())
        {
            return lineResults;
        }

        var xt = xPosThrethold * workingBitmap.PixelWidth;
        var yt = yPosThrethold * workingBitmap.PixelHeight;

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

        if (needScale)
        {
            workingBitmap.Dispose();
        }

        return results.Select(r => ToTextRect(r, needScale))
            // マージ後に少なすぎる文字も認識ミス扱い
            .Where(w => w.Text.Length > 2)
            // 全部数字なら対象外
            .Where(w => !IsAllSymbolOrSpace().IsMatch(w.Text))
            .ToArray();
    }

    private async ValueTask<SoftwareBitmap> ResizeSoftwareBitmapAsync(SoftwareBitmap source, double scale)
    {
        using var l = this.logger.LogDebugTime("Resizing Bitmap");
        var newWidth = (uint)(source.PixelWidth * scale);
        var newHeight = (uint)(source.PixelHeight * scale);

        this.resizeStream.Seek(0);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, resizeStream);
        encoder.SetSoftwareBitmap(source);
        encoder.BitmapTransform.InterpolationMode = scale > 1 ? BitmapInterpolationMode.Cubic : BitmapInterpolationMode.Fant;
        encoder.BitmapTransform.ScaledWidth = newWidth;
        encoder.BitmapTransform.ScaledHeight = newHeight;
        await encoder.FlushAsync();
        this.resizeStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(resizeStream);
        return await decoder.GetSoftwareBitmapAsync(source.BitmapPixelFormat, source.BitmapAlphaMode);
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

    private TextRect ToTextRect(TempMergeRect combinedRect, bool needScale)
    {
        var (x, y, width, height, fontSize, _) = combinedRect;
        var text = combinedRect.Text;
        // 元の画像座標に変換（リサイズ時のみ）
        if (needScale)
        {
            x /= this.scale;
            y /= this.scale;
            width /= this.scale;
            height /= this.scale;
            fontSize /= this.scale;
        }
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

    private TextRect CalcRect(OcrLine line, double angle, double centerX, double centerY)
    {
        if (IsIgnoreLine().IsMatch(line.Text))
        {
            return TextRect.Empty;
        }
        // ワードのフィルタリング
        var words = FilterWords(line.Words)
            .Select(word => CorrectWord(word, angle, centerX, centerY))
            .ToArray();

        if (words.Length == 0)
        {
            return TextRect.Empty;
        }
        var text = IsSpaceLang(this.source)
            ? string.Join(" ", words.Select(w => w.Text))
            : string.Concat(words.Select(w => w.Text));
        var x = words.Select(w => w.X).Min();
        var y = words.Select(w => w.Y).Average();
        var width = words.Select(w => w.Right).Max() - words.Select(w => w.Left).Min();
        var height = words.Select(w => w.Bottom).Average() - words.Select(w => w.Top).Average();
        return new(text, x, y, width, height, height, false);
    }

    [GeneratedRegex(@"^[\s\p{S}\p{P}\d]+$")]
    private static partial Regex IsAllSymbolOrSpace();

    /// <summary>
    /// 認識ミスとして無視する文字列
    /// * 4文字以上aoeのみで構成されているかどうか
    /// </summary>
    [GeneratedRegex(@"^[aceo@]{3,}$")]
    private static partial Regex IsIgnoreLine();

    public void Dispose()
        => this.resizeStream.Dispose();
}

file record WordRect(string Text, double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

file static class Utility
{
    private static (double x, double y) RotatePoint(double x, double y, double angleInDegrees, double centerX, double centerY)
    {
        double angleInRadians = angleInDegrees * Math.PI / 180.0;
        double cos = Math.Cos(angleInRadians);
        double sin = Math.Sin(angleInRadians);

        double dx = x - centerX;
        double dy = y - centerY;

        double xNew = dx * cos - dy * sin + centerX;
        double yNew = dx * sin + dy * cos + centerY;

        return new(xNew, yNew);
    }

    public static (double x, double y, double width, double height) RotateRect(Rect rect, double angleInDegrees, double centerX, double centerY)
    {
        var topLeft = RotatePoint(rect.Left, rect.Top, angleInDegrees, centerX, centerY);
        var topRight = RotatePoint(rect.Right, rect.Top, angleInDegrees, centerX, centerY);
        var bottomLeft = RotatePoint(rect.Left, rect.Bottom, angleInDegrees, centerX, centerY);
        var bottomRight = RotatePoint(rect.Right, rect.Bottom, angleInDegrees, centerX, centerY);

        double x = Math.Min(topLeft.x, bottomLeft.x);
        double y = (topLeft.y + topRight.y) / 2;
        double width = Math.Max(topRight.x, bottomRight.x) - x;
        double height = (bottomLeft.y + bottomRight.y) / 2 - y;

        return (x, y, width, height);
    }
    public static WordRect CorrectWord(OcrWord word, double angle, double centerX, double centerY)
    {
        // 文字種類の取得
        var (isxHeight, hasAcent, hasHarfAcent, hasDecent) = GetTextType(word.Text);

        // 矩形の回転補正
        var (x, y, width, height) = RotateRect(word.BoundingRect, angle, centerX, centerY);

        // 文字種類による位置補正
        y -= (hasAcent, hasHarfAcent) switch
        {
            (true, _) => 0,
            (false, true) => height * .1,
            (false, false) => height * .2,
        };

        // 文字種類による高さ補正
        height = CorrectHeight(height, isxHeight, hasAcent, hasHarfAcent, hasDecent);
        return new(word.Text, x, y, width, height);
    }

    public static IEnumerable<OcrWord> FilterWords(IEnumerable<OcrWord> words)
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
            (false, _, _, true) => height,
            (false, _, _, false) => height * 1.2,
        };

    private static (bool isxHeight, bool hasAcent, bool hasHarfAcent, bool hasDecent) GetTextType(string text)
    {
        // abcdefghijklmnopqrstuvwxyz
        // ABCDEFGHIJKLMNOPQRSTUVWXYZ
        var isxHeight = Contains(text, "acemnosuvwxz<>+=");
        var hasAcent = Contains(text, "ABCDEFGHIJKLMNOPQRSTUVWXYZbdfhijkl!\"#$%&'()|/[]{}@");
        var hasHarfAcent = Contains(text, "t^");
        var hasDecent = Contains(text, "gjpqy()|[]{}@");
        return (isxHeight, hasAcent, hasHarfAcent, hasDecent);
    }

    private static bool Contains(string text, string target)
    {
        ReadOnlySpan<char> te = text;
        ReadOnlySpan<char> ta = target;
        return te.ContainsAny(ta);
    }

    private static bool IsAllSameChar(string text)
    {
        ReadOnlySpan<char> chars = text;
        return !chars[1..].ContainsAnyExcept(chars[0]);
    }
}