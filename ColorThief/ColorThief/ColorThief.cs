using System.Buffers;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using WinRT;

namespace StudioFreesia.ColorThief;

public static class ColorThief
{
    public const int DefaultColorCount = 5;
    public const int DefaultQuality = 10;
    public const bool DefaultIgnoreWhite = true;
    public const int ColorDepth = 4;

    /// <summary>
    ///     Use the median cut algorithm to cluster similar colors.
    /// </summary>
    /// <param name="sourceImage">The source image.</param>
    /// <param name="colorCount">The color count.</param>
    /// <param name="quality">
    ///     1 is the highest quality settings. 10 is the default. There is
    ///     a trade-off between quality and speed. The bigger the number,
    ///     the faster a color will be returned but the greater the
    ///     likelihood that it will not be the visually most dominant color.
    /// </param>
    /// <param name="ignoreWhite">if set to <c>true</c> [ignore white].</param>
    /// <returns></returns>
    /// <code>true</code>
    public static List<QuantizedColor> GetPalette(SoftwareBitmap sourceImage, int colorCount = DefaultColorCount, int quality = DefaultQuality, bool ignoreWhite = DefaultIgnoreWhite)
        => GetPalette(sourceImage, Rectangle.Empty, colorCount, quality, ignoreWhite);

    /// <summary>
    ///     Use the median cut algorithm to cluster similar colors.
    /// </summary>
    /// <param name="sourceImage">The source image.</param>
    /// <param name="colorCount">The color count.</param>
    /// <param name="quality">
    ///     1 is the highest quality settings. 10 is the default. There is
    ///     a trade-off between quality and speed. The bigger the number,
    ///     the faster a color will be returned but the greater the
    ///     likelihood that it will not be the visually most dominant color.
    /// </param>
    /// <param name="ignoreWhite">if set to <c>true</c> [ignore white].</param>
    /// <returns></returns>
    /// <code>true</code>
    public static List<QuantizedColor> GetPalette(SoftwareBitmap sourceImage, Rectangle rect, int colorCount = DefaultColorCount, int quality = DefaultQuality, bool ignoreWhite = DefaultIgnoreWhite)
    {
        if (rect == Rectangle.Empty)
        {
            rect = new(0, 0, sourceImage.PixelWidth, sourceImage.PixelHeight);
        }
        if (quality < 1)
        {
            quality = DefaultQuality;
        }
        var pixelCount = rect.Width * rect.Height;
        var numRegardedPixels = (pixelCount + quality - 1) / quality;
        using var pixels = MemoryPool<RGB>.Shared.Rent(numRegardedPixels);

        var target = GetPixelsFast(sourceImage, rect, pixels.Memory, quality, ignoreWhite);
        var cmap = Mmcq.Quantize(target, --colorCount);
        return cmap.GeneratePalette();
    }

    unsafe private static Memory<RGB> GetPixelsFast(SoftwareBitmap bmp, Rectangle rect, Memory<RGB> mem, int quality, bool ignoreWhite)
    {
        using var buffer = bmp.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();
        reference.As<IMemoryBufferByteAccess>().GetBuffer(out var data, out _);

        // スキャンラインの長さとピクセルごとのサイズを取得
        var bufferLayout = buffer.GetPlaneDescription(0);
        const int bytesPerPixel = 4; // BGRA8フォーマットの場合

        var numUsedPixels = 0;
        var span = mem.Span;

        // ループの順序を変更してキャッシュの局所性を向上
        var mod = 0;
        for (var y = rect.Top; y < rect.Bottom; y++)
        {
            int rowStartIndex = bufferLayout.StartIndex + (y * bufferLayout.Stride) + (rect.Left * bytesPerPixel);
            mod %= rect.Width;
            int pixelIndex = rowStartIndex;
            for (; mod < rect.Width; mod += quality)
            {
                pixelIndex = rowStartIndex + (mod * bytesPerPixel);
                var b = data[pixelIndex++];
                var g = data[pixelIndex++];
                var r = data[pixelIndex++];
                var a = data[pixelIndex++];

                // If pixel is mostly opaque and not white
                if (a >= 125 && !(ignoreWhite && r > 250 && g > 250 && b > 250))
                {
                    span[numUsedPixels++] = new(r, g, b);
                }
            }
        }

        return mem[..numUsedPixels];
    }
}

// Using the COM interface IMemoryBufferByteAccess allows us to access the underlying byte array in an AudioFrame
[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}

readonly record struct RGB(byte R, byte G, byte B);