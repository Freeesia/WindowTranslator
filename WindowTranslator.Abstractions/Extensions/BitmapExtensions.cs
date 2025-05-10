#if WINDOWS
using System.Buffers;
using System.Drawing;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace WindowTranslator.Extensions;

/// <summary>
/// 画像関連の拡張メソッド
/// </summary>
public static class BitmapExtensions
{
    private static readonly ThreadLocal<InMemoryRandomAccessStream> stream = new(() => new());

    /// <summary>
    /// ソフトウェアビットマップをJPEG形式でエンコードし、Base64文字列に変換します。
    /// </summary>
    /// <param name="bitmap">エンコードするソフトウェアビットマップ。</param>
    /// <param name="rect">エンコードする領域。</param>
    /// <returns>Base64文字列。</returns>
    public static async Task<string> EncodeToJpegBase64(this SoftwareBitmap bitmap, Rectangle rect = default)
    {
        var (mem, size) = await bitmap.EncodeToJpeg(rect).ConfigureAwait(false);
        using var _ = mem;
        var buffer = mem.Memory[..size];
        return Convert.ToBase64String(buffer.Span);
    }

    /// <summary>
    /// ソフトウェアビットマップをJPEG形式でエンコードし、バイト配列に変換します。
    /// </summary>
    /// <param name="bitmap">エンコードするソフトウェアビットマップ。</param>
    /// <param name="rect">エンコードする領域。</param>
    /// <returns>Jpeg形式のバイト配列。</returns>
    public static async Task<byte[]> EncodeToJpegBytes(this SoftwareBitmap bitmap, Rectangle rect = default)
    {
        var (mem, size) = await bitmap.EncodeToJpeg(rect).ConfigureAwait(false);
        using var _ = mem;
        var buffer = mem.Memory[..size];
        return buffer.ToArray();
    }

    /// <summary>
    /// ソフトウェアビットマップをJPEG形式でエンコードします。
    /// </summary>
    /// <param name="bitmap">エンコードするソフトウェアビットマップ。</param>
    /// <param name="rect">エンコードする領域。</param>
    /// <returns>エンコードしたデータとサイズ</returns>
    public static async Task<(IMemoryOwner<byte> memory, int size)> EncodeToJpeg(this SoftwareBitmap bitmap, Rectangle rect = default)
    {
        var s = stream.Value!;
        s.Seek(0);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, s);
        encoder.SetSoftwareBitmap(bitmap);
        if (rect != default)
        {
            encoder.BitmapTransform.Bounds = new((uint)rect.X, (uint)rect.Y, (uint)rect.Width, (uint)rect.Height);
        }
        await encoder.FlushAsync();
        s.Seek(0);
        var mem = MemoryPool<byte>.Shared.Rent((int)s.Size);
        var buffer = mem.Memory[..(int)s.Size];
        await s.AsStreamForRead().ReadExactlyAsync(buffer).ConfigureAwait(false);
        return (mem, (int)s.Size);
    }

    /// <summary>
    /// TextRect オブジェクトを System.Drawing.Rectangle に変換します。
    /// </summary>
    /// <param name="rect">変換する TextRect オブジェクト。</param>
    /// <returns>変換された System.Drawing.Rectangle オブジェクト。</returns>
    public static Rectangle ToRect(this TextRect rect)
        => new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
}
#endif
