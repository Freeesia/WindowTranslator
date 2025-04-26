using System.Buffers;
using System.Drawing;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

static class Extensions
{
    private static readonly ThreadLocal<InMemoryRandomAccessStream> stream = new(() => new());

    public static async Task<string> EncodeToJpegBase64(this SoftwareBitmap bitmap, Rectangle rect = default)
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
        using var mem = MemoryPool<byte>.Shared.Rent((int)s.Size);
        var buffer = mem.Memory[..(int)s.Size];
        await s.AsStreamForRead().ReadExactlyAsync(buffer).ConfigureAwait(false);
        return Convert.ToBase64String(buffer.Span);
    }

    public static Rectangle ToRect(this TextRect rect)
        => new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
}