using Windows.Graphics.Imaging;

namespace WindowTranslator;

/// <summary>
/// OCRモジュールで優先矩形を処理するためのユーティリティ
/// </summary>
public static class PriorityRectUtility
{
    /// <summary>
    /// 画像を切り出す
    /// </summary>
    /// <param name="source">元の画像</param>
    /// <param name="rect">切り出す矩形（絶対座標）</param>
    /// <returns>切り出された画像</returns>
    public static async Task<SoftwareBitmap> CropBitmapAsync(SoftwareBitmap source, RectInfo rect)
    {
        var x = (int)Math.Max(0, rect.X);
        var y = (int)Math.Max(0, rect.Y);
        var width = (int)Math.Min(rect.Width, source.PixelWidth - x);
        var height = (int)Math.Min(rect.Height, source.PixelHeight - y);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Invalid rectangle dimensions");
        }

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

    /// <summary>
    /// TextRectの座標をオフセット分移動する
    /// </summary>
    /// <param name="rect">元のTextRect</param>
    /// <param name="offsetX">X方向のオフセット</param>
    /// <param name="offsetY">Y方向のオフセット</param>
    /// <param name="keyword">キーワード（コンテキスト）</param>
    /// <returns>オフセットされたTextRect</returns>
    public static TextRect OffsetTextRect(TextRect rect, double offsetX, double offsetY, string keyword = "")
    {
        return rect with
        {
            X = rect.X + offsetX,
            Y = rect.Y + offsetY,
            Context = keyword
        };
    }
}

[System.Runtime.InteropServices.ComImport]
[System.Runtime.InteropServices.Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}
