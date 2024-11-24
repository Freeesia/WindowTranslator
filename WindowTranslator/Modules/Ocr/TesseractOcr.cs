using System.Buffers;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractOCR.Layout;
using TesseractOCR.Pix;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WindowTranslator.Extensions;
using static WindowTranslator.Modules.Ocr.OcrUtility;

namespace WindowTranslator.Modules.Ocr;

public sealed class TesseractOcr(IOptionsSnapshot<LanguageOptions> langOptions, ILogger<TesseractOcr> logger) : IOcrModule, IDisposable
{
    public static string DataDir { get; } = Path.Combine(PathUtility.UserDir, "tessdata");
    private readonly ILogger<TesseractOcr> logger = logger;
    private readonly Engine engine = new(DataDir, ConvertLanguage(langOptions.Value.Source), EngineMode.Default, logger: logger);
    private readonly InMemoryRandomAccessStream stream = new();

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        using var t = this.logger.LogDebugTime("OCR Recognize");
        var (bytes, size) = await SoftwareToBytesAsync(bitmap);
        try
        {
            using var img = Image.LoadFromMemory(bytes, 0, size);
            using var page = engine.Process(img);
            var lineResults = page
                .Layout
                .SelectMany(l => l.Paragraphs)
                .SelectMany(p => p.TextLines)
                .SelectMany(t => t.Words)
                .Select(CalcRect)
                .Where(w => !string.IsNullOrEmpty(w.Text))
                .ToArray();

            return lineResults;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    public void Dispose()
    {
        this.stream.Dispose();
        this.engine.Dispose();
    }

    public static Language ConvertLanguage(string lang) => lang switch
    {
        "ja-JP" => Language.Japanese,
        "en-US" => Language.English, // EnglishMiddleなに？
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
        _ => Language.English,
    };

    private async ValueTask<(byte[] buf, int size)> SoftwareToBytesAsync(SoftwareBitmap softwareBitmap)
    {
        this.stream.Seek(0);
        // グレースケールに変換
        var grayBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Gray8, BitmapAlphaMode.Ignore);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, this.stream);
        encoder.SetSoftwareBitmap(grayBitmap);
        await encoder.FlushAsync();
        var size = (int)this.stream.Size;
        var bytes = ArrayPool<byte>.Shared.Rent(size);
        await this.stream.ReadAsync(bytes.AsBuffer(), (uint)size, InputStreamOptions.None);
        return (bytes, size);
    }

    private TextRect CalcRect(Word block)
    {
        var text = block.Text?.TrimEnd();
        if (string.IsNullOrEmpty(text) || IsIgnoreLine().IsMatch(text))
        {
            return TextRect.Empty;
        }
        var x = block.BoundingBox.Value.X1;
        var y = block.BoundingBox.Value.Y1;
        var width = block.BoundingBox.Value.Width;
        var height = block.BoundingBox.Value.Height;
        var fontSize = block.FontProperties.PointSize * 0.5;
        return new(text, x, y, width, height, fontSize, false);
    }
}
