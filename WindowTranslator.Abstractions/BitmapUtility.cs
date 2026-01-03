#if WINDOWS
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace WindowTranslator;

/// <summary>
/// 画像関連のユーティリティクラス
/// </summary>
public static class BitmapUtility
{
    private static readonly AsyncLocal<InMemoryRandomAccessStream> streamCache = new();

    /// <summary>
    /// 画像のリサイズを行う
    /// </summary>
    /// <param name="source">元画像</param>
    /// <param name="scale">拡大率</param>
    /// <param name="token">キャンセルトークン</param>
    /// <returns>リサイズ後の画像</returns>
    public static async ValueTask<SoftwareBitmap> ResizeSoftwareBitmapAsync(this SoftwareBitmap source, double scale, CancellationToken token = default)
    {
        var newWidth = (uint)(source.PixelWidth * scale);
        var newHeight = (uint)(source.PixelHeight * scale);

        if (newWidth == source.PixelWidth && newHeight == source.PixelHeight)
        {
            return source;
        }

        var resizeStream = streamCache.Value ??= new();

        resizeStream.Seek(0);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, resizeStream);
        token.ThrowIfCancellationRequested();
        encoder.SetSoftwareBitmap(source);
        encoder.BitmapTransform.InterpolationMode = scale > 1 ? BitmapInterpolationMode.Cubic : BitmapInterpolationMode.Fant;
        encoder.BitmapTransform.ScaledWidth = newWidth;
        encoder.BitmapTransform.ScaledHeight = newHeight;
        await encoder.FlushAsync();
        token.ThrowIfCancellationRequested();
        resizeStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(resizeStream);
        token.ThrowIfCancellationRequested();
        return await decoder.GetSoftwareBitmapAsync(source.BitmapPixelFormat, source.BitmapAlphaMode);
    }

    /// <summary>
    /// 画像を指定されたパスに保存する
    /// </summary>
    /// <remarks>
    /// エラー解析用のため、保存に失敗しても例外はスローしません。
    /// </remarks>
    /// <param name="source">保存する画像</param>
    /// <param name="path">保存先のパス</param>
    /// <returns>非同期操作</returns>
    public static async ValueTask TrySaveImage(this SoftwareBitmap source, string path)
    {
        try
        {
            // ディレクトリが存在しない場合は作成
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 拡張子からフォーマットを特定
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var encoderId = extension switch
            {
                ".jpg" or ".jpeg" => BitmapEncoder.JpegEncoderId,
                ".png" => BitmapEncoder.PngEncoderId,
                ".bmp" => BitmapEncoder.BmpEncoderId,
                ".tif" or ".tiff" => BitmapEncoder.TiffEncoderId,
                ".gif" => BitmapEncoder.GifEncoderId,
                ".heic" or ".heif" => BitmapEncoder.HeifEncoderId,
                _ => BitmapEncoder.JpegEncoderId // デフォルトはJPEG
            };

            // ファイルを作成
            using var fileStream = new FileStream(path, FileMode.Create);
            // IRandomAccessStreamに変換
            using var randomAccessStream = fileStream.AsRandomAccessStream();

            // エンコーダーを作成
            var encoder = await BitmapEncoder.CreateAsync(encoderId, randomAccessStream);

            // ビットマップをセット
            encoder.SetSoftwareBitmap(source);

            // フラッシュして保存
            await encoder.FlushAsync();
        }
        catch (Exception)
        {
            // エラー解析用のため、例外はスローしない
            // ここで何かログを残すことも可能ですが、今回は省略します
        }
    }

    /// <summary>
    /// 画像を切り出す
    /// </summary>
    /// <param name="source">元の画像</param>
    /// <param name="rect">切り出す矩形（絶対座標）</param>
    /// <returns>切り出された画像</returns>
    public unsafe static SoftwareBitmap Crop(this SoftwareBitmap source, RectInfo rect)
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

        sourceReference.As<IMemoryBufferByteAccess>().GetBuffer(out var sourceData, out var sourceCapacity);
        croppedReference.As<IMemoryBufferByteAccess>().GetBuffer(out var croppedData, out var croppedCapacity);

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

        return cropped;
    }

}


[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
file unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}
#endif
