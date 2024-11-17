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
using static WindowTranslator.Modules.Ocr.OcrUtility;

namespace WindowTranslator.Modules.Ocr;

public sealed class TesseractOcr(IOptionsSnapshot<LanguageOptions> langOptions, ILogger<TesseractOcr> logger) : IOcrModule, IDisposable
{
    private readonly string source = langOptions.Value.Source;
    private readonly ILogger<TesseractOcr> logger = logger;
    private readonly Engine engine = new(Path.Combine(PathUtility.UserDir, "tessdata"), Language.English, EngineMode.Default, logger: logger);
    private readonly IRandomAccessStream stream = new InMemoryRandomAccessStream();

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        var (bytes, size) = await SoftwareToBytesAsync(bitmap);
        try
        {
            using var img = Image.LoadFromMemory(bytes, 0, size);
            using var page = engine.Process(img);
            var lineResults = page
                .Layout
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

    private async ValueTask<(byte[] buf, int size)> SoftwareToBytesAsync(SoftwareBitmap softwareBitmap)
    {
        this.stream.Seek(0);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, this.stream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();
        var size = (int)this.stream.Size;
        var bytes = ArrayPool<byte>.Shared.Rent(size);
        await this.stream.ReadAsync(bytes.AsBuffer(), (uint)size, InputStreamOptions.None);
        return (bytes, size);
    }

    private TextRect CalcRect(Block block)
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
        return new(text, x, y, width, height, fontSize, block.Paragraphs.Sum(p => p.TextLines.Count()) > 1);
    }
}
