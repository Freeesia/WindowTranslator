using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WindowTranslator.ComponentModel;
using WindowTranslator.Extensions;
using static WindowTranslator.LanguageUtility;
using static WindowTranslator.Modules.Ocr.Utility;
using static WindowTranslator.Modules.Ocr.WindowsMediaOcrUtility;
using static WindowTranslator.OcrUtility;

namespace WindowTranslator.Modules.Ocr;

public sealed partial class WindowsMediaOcr(
    IOptionsSnapshot<LanguageOptions> langOptions,
    IOptionsSnapshot<BasicOcrParam> ocrParam,
    ILogger<WindowsMediaOcr> logger)
    : IOcrModule, IDisposable
{
    private readonly BasicOcrParam ocrOptions = ocrParam.Value;
    private readonly string source = langOptions.Value.Source;
    private readonly double scale = ocrParam.Value.Scale;
    private readonly int brightness = ocrParam.Value.Brightness;
    private readonly int contrast = ocrParam.Value.Contrast;
    private readonly OcrEngine ocr = OcrEngine.TryCreateFromLanguage(new(ConvertLanguage(langOptions.Value.Source)))
            ?? throw new AppUserException(string.Format(Properties.Resources.OcrLanguageNotAvailable, langOptions.Value.Source));
    private readonly ILogger<WindowsMediaOcr> logger = logger;
    private readonly InMemoryRandomAccessStream resizeStream = new();
    private readonly CancellationTokenSource cts = new();

    public ValueTask<IReadOnlyList<TextRect>> RecognizeAsync(OcrCaptureInput input)
    {
        foreach (var region in input.Regions)
        {
            var width = (uint)(region.Bounds.Width * this.scale);
            var height = (uint)(region.Bounds.Height * this.scale);
            if (width > OcrEngine.MaxImageDimension || height > OcrEngine.MaxImageDimension)
            {
                throw new AppUserException($"ウィンドウサイズが大きすぎます。対象ウィンドウのサイズを小さくするか、認識設定の拡大率を下げてください。actual:({width},{height}), max:{OcrEngine.MaxImageDimension}");
            }
        }

        return OcrUtility.RecognizeRegionsAsync(
            input,
            RecognizeRegionAsync,
            this.scale,
            this.brightness,
            this.contrast,
            this.cts.Token);
    }

    /// <summary>
    /// 指定した画像のテキストを認識する
    /// </summary>
    /// <param name="workingBitmap">認識対象の画像</param>
    /// <param name="sourceSize">拡大後の全体画像サイズ</param>
    private async ValueTask<IReadOnlyList<TextRect>> RecognizeRegionAsync(SoftwareBitmap workingBitmap, System.Drawing.Size sourceSize)
    {
        var t = this.logger.LogDebugTime("OCR Recognize");
        var rawResults = await ocr.RecognizeAsync(workingBitmap);
        this.cts.Token.ThrowIfCancellationRequested();
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


        // Windows OCRの矩形は角度補正済み座標なので、結合後に画像中心で回転する。
        var lineResults = rawResults
            .Lines
            .Select(line => CalcRect(line, 0, centerX, centerY))
            // 大きすぎる文字は映像の認識ミスとみなす
            .Where(w => w.Height < sourceSize.Height * 0.1)
            .ToArray();

        if (lineResults.IsEmpty())
        {
            return lineResults;
        }

        return OcrUtility.MergeTextRects(lineResults, sourceSize, this.ocrOptions, this.source)
            .Select(r => ToTextRect(r, angle, centerX, centerY))
            // マージ後に少なすぎる文字も認識ミス扱い
            // 特殊なグリフの言語は対象外(日本語、中国語、韓国語、ロシア語)
            .Where(w => IsSpecialLang(this.source) || w.SourceText.Length > 2)
            // 全部数字なら対象外
            .Where(w => !AllSymbolOrSpace().IsMatch(w.SourceText))
            .ToArray();
    }

    private static TextRect ToTextRect(TextRect combinedRect, double angle, double centerX, double centerY)
    {
        var (_, x, y, width, height, fontSize, _, _, _) = combinedRect;
        // 若干太らせて完全に文字を覆う
        const double fat = .2;
        width += fontSize * fat;
        x -= fontSize * fat * .5;
        height += fontSize * fat;
        y -= fontSize * fat * .5;

        (x, y) = RotatePoint(x, y, angle, centerX, centerY);
        return combinedRect with { X = x, Y = y, Width = width, Height = height, Angle = angle };
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
        return new(text, x, y, width, height, height, false) { Angle = angle };
    }

    /// <summary>
    /// 認識ミスとして無視する文字列
    /// * 4文字以上aoeのみで構成されているかどうか
    /// </summary>
    [GeneratedRegex(@"^[aceo@]{3,}$")]
    private static partial Regex IsIgnoreLine();

    public void Dispose()
    {
        this.cts.Cancel();
        this.resizeStream.Dispose();
    }
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
    public static (double x, double y) RotatePoint(double x, double y, double angleInDegrees, double centerX, double centerY)
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
        // 矩形の回転補正
        var (x, y, width, height) = RotateRect(word.BoundingRect, angle, centerX, centerY);

        // CJK文字を含む単語にはラテン文字向けの位置・高さ補正を適用しない
        if (WindowsMediaOcrUtility.ContainsCjk(word.Text))
        {
            return new(word.Text, x, y, width, height);
        }

        // 文字種類の取得
        var (isxHeight, hasAcent, hasHarfAcent, hasDecent) = GetTextType(word.Text);

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
        words = words.Where(w => w.Text.Replace("•", string.Empty) is { } t && (t.Length < 3 || !IsAllSameChar(t)));
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
