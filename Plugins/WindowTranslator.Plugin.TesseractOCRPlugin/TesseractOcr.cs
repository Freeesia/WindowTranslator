using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Layout;
using TesseractOCR.Pix;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using static WindowTranslator.LanguageUtility;
using static WindowTranslator.OcrUtility;

namespace WindowTranslator.Plugin.TesseractOCRPlugin;

[DefaultModule]
public sealed class TesseractOcr(
    IOptionsSnapshot<LanguageOptions> langOptions,
    IOptionsSnapshot<BasicOcrParam> ocrParam,
    ILogger<TesseractOcr> logger) : IOcrModule, IDisposable
{
    public static readonly string DataDir = Path.Combine(PathUtility.UserDir, "tessdata");
    private readonly ILogger<TesseractOcr> logger = logger;
    private readonly Engine engine = new(DataDir, ConvertLanguage(langOptions.Value.Source), EngineMode.Default, logger: logger);
    private readonly InMemoryRandomAccessStream stream = new();
    private readonly CancellationTokenSource cts = new();

    // マージと除外処理のためのパラメータ
    private readonly BasicOcrParam ocrOptions = ocrParam.Value;
    private readonly string source = langOptions.Value.Source;
    private readonly double scale = ocrParam.Value.Scale;
    private readonly int brightness = ocrParam.Value.Brightness;
    private readonly int contrast = ocrParam.Value.Contrast;

    public ValueTask<IReadOnlyList<TextRect>> RecognizeAsync(OcrCaptureInput input)
        => OcrUtility.RecognizeRegionsAsync(
            input,
            RecognizeRegionAsync,
            this.scale,
            this.brightness,
            this.contrast,
            this.cts.Token);

    /// <summary>
    /// 指定した画像のテキストを認識する
    /// </summary>
    /// <param name="bitmap">認識対象の画像</param>
    /// <param name="sourceSize">拡大後の全体画像サイズ</param>
    private async ValueTask<IReadOnlyList<TextRect>> RecognizeRegionAsync(SoftwareBitmap bitmap, System.Drawing.Size sourceSize)
    {

        var sw = Stopwatch.StartNew();
        // テキスト認識処理をバックグラウンドで実行
        var textRects = await Task.Run(async () => await Recognize(bitmap).ConfigureAwait(false), this.cts.Token).ConfigureAwait(false);
        this.cts.Token.ThrowIfCancellationRequested();
        this.logger.LogDebug($"Recognize: {sw.Elapsed}");

        if (textRects.Length == 0)
        {
            return textRects;
        }

        return OcrUtility.MergeTextRects(textRects, sourceSize, this.ocrOptions, this.source)
            .Select(ToTextRect)
            // マージ後に少なすぎる文字も認識ミス扱い
            // 特殊なグリフの言語は対象外(日本語、中国語、韓国語、ロシア語)
            .Where(w => IsSpecialLang(this.source) || w.SourceText.Length > 2)
            // 全部数字・記号なら対象外
            .Where(w => !AllSymbolOrSpace().IsMatch(w.SourceText))
            .ToArray();
    }

    private async ValueTask<TextRect[]> Recognize(SoftwareBitmap bitmap)
    {
        using var buf = await SoftwareToBytesAsync(bitmap);
        this.cts.Token.ThrowIfCancellationRequested();

        using var img = Image.LoadFromMemory(buf.Bytes, 0, buf.Size);
        this.cts.Token.ThrowIfCancellationRequested();

        lock (this.engine)
        {
            using var page = engine.Process(img);
            this.cts.Token.ThrowIfCancellationRequested();

            // 基本的な認識処理
            return page
                .Layout
                .SelectMany(l => l.Paragraphs)
                .SelectMany(p => p.TextLines)
                .Select(line =>
                {
                    // エンジンが認識した同一行の情報を保持して段落結合へ渡す。
                    var words = line.Words.Select(CalcRect).Where(r => !string.IsNullOrEmpty(r.SourceText)).ToArray();
                    if (words.Length == 0)
                        return TextRect.Empty;
                    var left = words.Min(r => r.X);
                    var top = words.Min(r => r.Y);
                    return new TextRect(
                        string.Join(IsSpaceLang(this.source) ? " " : string.Empty, words.Select(r => r.SourceText)),
                        left, top, words.Max(r => r.X + r.Width) - left, words.Max(r => r.Y + r.Height) - top,
                        words.Average(r => r.FontSize), false);
                })
                .Where(w => !string.IsNullOrEmpty(w.SourceText))
                .ToArray();
        }
    }

    private static TextRect ToTextRect(TextRect combinedRect)
    {
        var (_, x, y, width, height, fontSize, _, _, _) = combinedRect;

        // 若干太らせて完全に文字を覆う
        const double fat = .4;
        width += fontSize * fat;
        x -= fontSize * fat * .5;
        height += fontSize * fat;
        y -= fontSize * fat * .5;

        return combinedRect with { X = x, Y = y, Width = width, Height = height };
    }

    public void Dispose()
    {
        this.cts.Cancel();
        stream.Dispose();
        lock (this.engine)
        {
            engine.Dispose();
        }
        this.cts.Dispose();
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

    private static void MigrateModelsIfNeeded()
    {
        var oldPath = Path.Combine(PathUtility.UserDir, "tessdata");
        if (Directory.Exists(oldPath) && !Directory.Exists(DataDir))
        {
            Directory.CreateDirectory(PathUtility.SharedDir);
            Directory.Move(oldPath, DataDir);
        }
    }

    [ModuleInitializer]
    internal static void Initialize()
    {
        MigrateModelsIfNeeded();
    }
}
