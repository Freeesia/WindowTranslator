using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Layout;
using TesseractOCR.Pix;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WindowTranslator.Collections;
using WindowTranslator.Modules;
using static WindowTranslator.LanguageUtility;
using static WindowTranslator.OcrUtility;

namespace WindowTranslator.Plugin.TesseractOCRPlugin;

public sealed class TesseractOcr(
    IOptionsSnapshot<LanguageOptions> langOptions,
    IOptionsSnapshot<BasicOcrParam> ocrParam,
    ILogger<TesseractOcr> logger) : IOcrModule, IDisposable
{
    public static readonly string DataDir = Path.Combine(PathUtility.UserDir, "tessdata");
    private readonly ILogger<TesseractOcr> logger = logger;
    private readonly Engine engine = new(DataDir, ConvertLanguage(langOptions.Value.Source), EngineMode.Default, logger: logger);
    private readonly InMemoryRandomAccessStream stream = new();

    // マージと除外処理のためのパラメータ
    private readonly double xPosThreshold = ocrParam.Value.XPosThrethold;
    private readonly double yPosThreshold = ocrParam.Value.YPosThrethold;
    private readonly double leadingThreshold = ocrParam.Value.LeadingThrethold;
    private readonly double spacingThreshold = ocrParam.Value.SpacingThreshold;
    private readonly double fontSizeThreshold = ocrParam.Value.FontSizeThrethold;
    private readonly bool isAvoidMergeList = ocrParam.Value.IsAvoidMergeList;
    private readonly string source = langOptions.Value.Source;
    private readonly double scale = ocrParam.Value.Scale;

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        // 拡大率に基づくリサイズ処理
        var workingBitmap = await bitmap.ResizeSoftwareBitmapAsync(this.scale);

        var sw = Stopwatch.StartNew();
        // テキスト認識処理をバックグラウンドで実行
        var textRects = await Task.Run(async () => await Recognize(workingBitmap).ConfigureAwait(false)).ConfigureAwait(false);
        this.logger.LogDebug($"Recognize: {sw.Elapsed}");

        if (textRects.Length == 0)
        {
            return textRects;
        }

        // マージ処理
        var xt = xPosThreshold * bitmap.PixelWidth;
        var yt = yPosThreshold * bitmap.PixelHeight;

        var results = new List<TempMergeRect>(textRects.Length);
        var queue = new RemovableQueue<TextRect>(textRects.OrderBy(r => r.Y));

        while (queue.TryDequeue(out var target))
        {
            var temp = new TempMergeRect(this.source, target);
            var merged = false;
            do
            {
                merged = false;
                foreach (var wordResult in queue.ToArray())
                {
                    if (CanMerge(temp, wordResult, xt, yt))
                    {
                        temp.Merge(wordResult);
                        queue.Remove(wordResult);
                        merged = true;
                    }
                }
                foreach (var rect in results.OrderBy(r => r.Y).ToArray())
                {
                    var r = rect.ToRect();
                    if (CanMerge(temp, r, xt, yt))
                    {
                        temp.Merge(r);
                        results.Remove(rect);
                        merged = true;
                    }
                }
            } while (merged);
            results.Add(temp);
        }

        if (bitmap != workingBitmap)
        {
            workingBitmap.Dispose();
        }

        return results
            .Select(r => ToTextRect(r, this.scale))
            // マージ後に少なすぎる文字も認識ミス扱い
            // 特殊なグリフの言語は対象外(日本語、中国語、韓国語、ロシア語)
            .Where(w => IsSpecialLang(this.source) || w.SourceText.Length > 2)
            // 全部数字・記号なら対象外
            .Where(w => !AllSymbolOrSpace().IsMatch(w.SourceText));
    }

    private async ValueTask<TextRect[]> Recognize(SoftwareBitmap bitmap)
    {
        using var buf = await SoftwareToBytesAsync(bitmap);
        using var img = Image.LoadFromMemory(buf.Bytes, 0, buf.Size);
        using var page = engine.Process(img);

        // 基本的な認識処理
        return page
            .Layout
            .SelectMany(l => l.Paragraphs)
            .SelectMany(p => p.TextLines)
            .SelectMany(t => t.Words)
            .Select(CalcRect)
            .Where(w => !string.IsNullOrEmpty(w.SourceText))
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
        if (fDiff > fontSizeThreshold)
        {
            return false;
        }

        var fontSize = temp.Rects.Append(rect).Average(r => r.FontSize);
        var (text, x, y, w, _, _, _, _, _) = rect;

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
        var lThre = fontSize * leadingThreshold; // 行間の閾値
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
        private string? cachedText;
        private readonly List<TextRect> rects;
        private bool textDirty = true;

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
        public string Text
        {
            get
            {
                if (textDirty || cachedText == null)
                {
                    cachedText = CreateText();
                    textDirty = false;
                }
                return cachedText;
            }
        }

        public TempMergeRect(string lang, TextRect rect)
        {
            this.lang = lang;
            (_, X, Y, Width, Height, FontSize, _, _, _) = rect;
            this.rects = [rect];
        }

        public void Merge(TextRect rect)
        {
            var (_, x, y, width, height, _, _, _, _) = rect;
            this.rects.Add(rect);
            var x1 = Math.Min(X, x);
            var y1 = Math.Min(Y, y);
            var x2 = Math.Max(X + Width, x + width);
            var y2 = Math.Max(Y + Height, y + height);
            (X, Y, Width, Height) = (x1, y1, x2 - x1, y2 - y1);
            FontSize = Rects.Average(r => r.FontSize);
            textDirty = true;
        }

        public bool IntersectsWith(TextRect rect)
            => (rect.X < X + Width) && (X < rect.X + rect.Width) && (rect.Y < Y + Height) && (Y < rect.Y + rect.Height);

        private string CreateText()
        {
            var builder = new StringBuilder(this.Rects.Sum(r => r.SourceText.Length + 1));
            foreach (var rect in this.Rects.OrderBy(r => (int)((r.Y - this.Y) / this.FontSize)).ThenBy(r => r.X))
            {
                builder.Append(rect.SourceText);
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

        public TextRect ToRect()
        {
            // 元の画像座標に変換
            var text = this.Text;
            // 高さがフォントサイズの2倍以上の場合は複数行とみなす
            var lines = Height / FontSize >= 2;
            return new(text, X, Y, Width, Height, FontSize, lines);
        }
    }

    private static TextRect ToTextRect(TempMergeRect combinedRect, double scale)
    {
        var (x, y, width, height, fontSize, _) = combinedRect;
        var text = combinedRect.Text;
        // 元の画像座標に変換
        x /= scale;
        y /= scale;
        width /= scale;
        height /= scale;
        fontSize /= scale;

        // 高さがフォントサイズの2倍以上の場合は複数行とみなす
        var lines = height / fontSize >= 2;

        // 若干太らせて完全に文字を覆う
        const double fat = .2;
        width += fontSize * fat;
        x -= fontSize * fat * .5;
        height += fontSize * fat;
        y -= fontSize * fat * 1.5;

        return new(text, x, y, width, height, fontSize, lines);
    }

    public void Dispose()
    {
        stream.Dispose();
        engine.Dispose();
    }

    public static Language ConvertLanguage(string lang) => lang switch
    {
        "ja-JP" => Language.Japanese,
        "en-US" => Language.English,
        "pt-BR" => Language.Portuguese,
        "fr-CA" => Language.French,
        "fr-FR" => Language.French,
        "it-IT" => Language.Italian,
        "de-DE" => Language.German,
        "es-ES" => Language.SpanishCastilian,
        "pt-PT" => Language.Portuguese,
        "nl-NL" => Language.Dutch,
        "ru-RU" => Language.Russian,
        "ko-KR" => Language.Korean,
        "zh-Hant" => Language.ChineseTraditional,
        "zh-Hans" => Language.ChineseSimplified,
        "vi-VI" => Language.Vietnamese,
        _ => Language.English,
    };

    private async ValueTask<RentBuf> SoftwareToBytesAsync(SoftwareBitmap softwareBitmap)
    {
        stream.Seek(0);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();
        var size = (int)stream.Size;
        var mem = new RentBuf(size);
        await stream.ReadAsync(mem.Bytes.AsBuffer(), (uint)size, InputStreamOptions.None);
        return mem;
    }

    private TextRect CalcRect(Word block)
    {
        var text = block.Text?.TrimEnd();
        if (string.IsNullOrEmpty(text))
        {
            return TextRect.Empty;
        }

        var bbox = block.BoundingBox;
        if (!bbox.HasValue)
        {
            return TextRect.Empty;
        }

        var x = bbox.Value.X1;
        var y = bbox.Value.Y1;
        var width = bbox.Value.Width;
        var height = bbox.Value.Height;
        var fontSize = block.FontProperties.PointSize;
        return new(text, x, y, width, height, fontSize, false);
    }

    private readonly record struct RentBuf(int Size) : IDisposable
    {
        public byte[] Bytes { get; } = ArrayPool<byte>.Shared.Rent(Size);
        public void Dispose() => ArrayPool<byte>.Shared.Return(this.Bytes);
    }
}
