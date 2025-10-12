using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Graphics.Imaging;
using WindowTranslator.Extensions;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// 優先矩形のOCR処理を行うフィルター
/// </summary>
public class PriorityRectFilter(
    IServiceProvider serviceProvider,
    IOptionsSnapshot<BasicOcrParam> options,
    ILogger<PriorityRectFilter> logger) : IFilterModule
{
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly ILogger<PriorityRectFilter> logger = logger;
    private readonly List<PriorityRect> priorityRects = options.Value.PriorityRects ?? [];

    /// <summary>
    /// フィルターの優先度（OCR直後、他のフィルターより前に実行）
    /// </summary>
    public double Priority => FilterPriority.PriorityRectFilter;

    public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
    {
        if (this.priorityRects.Count == 0)
        {
            // 優先矩形が設定されていない場合はそのまま返す
            await foreach (var text in texts)
            {
                yield return text;
            }
            yield break;
        }

        // 元のOCR結果をリスト化
        var originalTexts = await texts.ToArrayAsync();

        // IOcrModuleを取得
        var ocr = this.serviceProvider.GetRequiredService<IOcrModule>();

        // 優先矩形ごとにOCRを実行
        var priorityTexts = new List<(TextRect rect, int priority)>();
        
        for (int i = 0; i < this.priorityRects.Count; i++)
        {
            var priorityRect = this.priorityRects[i];
            var absRect = priorityRect.ToAbsoluteRect(context.ImageSize.Width, context.ImageSize.Height);

            // 矩形が画像範囲外の場合はスキップ
            if (absRect.X < 0 || absRect.Y < 0 || 
                absRect.X + absRect.Width > context.ImageSize.Width || 
                absRect.Y + absRect.Height > context.ImageSize.Height)
            {
                this.logger.LogWarning($"Priority rect {i} is out of image bounds, skipping");
                continue;
            }

            try
            {
                // 指定矩形の画像を切り出してOCR
                var croppedBitmap = await CropBitmapAsync(context.SoftwareBitmap, absRect);
                var rectTexts = await ocr.RecognizeAsync(croppedBitmap);
                croppedBitmap.Dispose();

                // 切り出した画像の座標を元の画像の座標に変換
                foreach (var text in rectTexts)
                {
                    var adjustedText = text with
                    {
                        X = text.X + absRect.X,
                        Y = text.Y + absRect.Y,
                        Context = priorityRect.Keyword
                    };
                    priorityTexts.Add((adjustedText, i));
                    this.logger.LogDebug($"Priority rect {i} OCR: {adjustedText.SourceText} at ({adjustedText.X}, {adjustedText.Y})");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Failed to OCR priority rect {i}");
            }
        }

        // 優先矩形の結果と重複する元のOCR結果を除外
        var filteredOriginalTexts = new List<TextRect>();
        foreach (var original in originalTexts)
        {
            bool overlaps = false;
            foreach (var (priorityText, _) in priorityTexts)
            {
                if (original.OverlapsWith(priorityText))
                {
                    overlaps = true;
                    this.logger.LogDebug($"Original text '{original.SourceText}' overlaps with priority text '{priorityText.SourceText}', removing original");
                    break;
                }
            }
            
            if (!overlaps)
            {
                filteredOriginalTexts.Add(original);
            }
        }

        // 優先度順にソートして返す（優先度の高い順、同じ優先度ならY座標順）
        var sortedPriorityTexts = priorityTexts
            .OrderBy(x => x.priority)
            .ThenBy(x => x.rect.Y)
            .Select(x => x.rect);

        // 優先矩形の結果を先に返す
        foreach (var text in sortedPriorityTexts)
        {
            yield return text;
        }

        // 残りの元のOCR結果を返す
        foreach (var text in filteredOriginalTexts)
        {
            yield return text;
        }
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
        => texts;

    /// <summary>
    /// 画像を切り出す
    /// </summary>
    private static async Task<SoftwareBitmap> CropBitmapAsync(SoftwareBitmap source, RectInfo rect)
    {
        var x = (int)Math.Max(0, rect.X);
        var y = (int)Math.Max(0, rect.Y);
        var width = (int)Math.Min(rect.Width, source.PixelWidth - x);
        var height = (int)Math.Min(rect.Height, source.PixelHeight - y);

        var cropped = new SoftwareBitmap(source.BitmapPixelFormat, width, height, source.BitmapAlphaMode);

        using var sourceBuffer = source.LockBuffer(BitmapBufferAccessMode.Read);
        using var croppedBuffer = cropped.LockBuffer(BitmapBufferAccessMode.Write);
        using var sourceReference = sourceBuffer.CreateReference();
        using var croppedReference = croppedBuffer.CreateReference();

        unsafe
        {
            byte* sourceData;
            uint sourceCapacity;
            ((IMemoryBufferByteAccess)sourceReference).GetBuffer(out sourceData, out sourceCapacity);

            byte* croppedData;
            uint croppedCapacity;
            ((IMemoryBufferByteAccess)croppedReference).GetBuffer(out croppedData, out croppedCapacity);

            var bytesPerPixel = 4; // BGRA8
            var sourceStride = sourceBuffer.GetPlaneDescription(0).Stride;
            var croppedStride = croppedBuffer.GetPlaneDescription(0).Stride;

            for (int row = 0; row < height; row++)
            {
                var sourceOffset = ((y + row) * sourceStride) + (x * bytesPerPixel);
                var croppedOffset = row * croppedStride;
                
                for (int col = 0; col < width * bytesPerPixel; col++)
                {
                    croppedData[croppedOffset + col] = sourceData[sourceOffset + col];
                }
            }
        }

        return await Task.FromResult(cropped);
    }
}

[System.Runtime.InteropServices.ComImport]
[System.Runtime.InteropServices.Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}
