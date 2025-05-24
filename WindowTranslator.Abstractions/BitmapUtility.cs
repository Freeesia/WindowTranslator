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
}
#endif
