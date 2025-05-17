#if WINDOWS
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;


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
}
#endif
