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
        var pixelArray = GetPixelsFast(sourceImage, rect, quality, ignoreWhite);
        var cmap = GetColorMap(pixelArray, colorCount);
        if (cmap != null)
        {
            var colors = cmap.GeneratePalette();
            return colors;
        }
        return [];
    }

    private static byte[][] GetPixelsFast(SoftwareBitmap sourceImage, Rectangle rect, int quality, bool ignoreWhite)
    {
        if (quality < 1)
        {
            quality = DefaultQuality;
        }

        var pixels = GetIntFromPixel(sourceImage, rect);
        var pixelCount = rect.Width * rect.Height;

        return ConvertPixels(pixels, pixelCount, quality, ignoreWhite);
    }

    unsafe private static byte[] GetIntFromPixel(SoftwareBitmap bmp, Rectangle rect)
    {
        using var buffer = bmp.LockBuffer(BitmapBufferAccessMode.Read);
        var pixelList = new byte[rect.Width * rect.Height * 4];
        var count = 0;

        using var reference = buffer.CreateReference();
        reference.As<IMemoryBufferByteAccess>().GetBuffer(out var data, out _);

        // スキャンラインの長さとピクセルごとのサイズを取得
        var bufferLayout = buffer.GetPlaneDescription(0);
        int bytesPerPixel = 4; // BGRA8フォーマットの場合

        for (var x = rect.Left; x < rect.Right; x++)
            for (var y = rect.Top; y < rect.Bottom; y++)
            {
                // 指定されたピクセルの位置を計算
                int pixelIndex = bufferLayout.StartIndex + (y * bufferLayout.Stride) + (x * bytesPerPixel);

                pixelList[count++] = data[pixelIndex];
                pixelList[count++] = data[pixelIndex + 1];
                pixelList[count++] = data[pixelIndex + 2];
                pixelList[count++] = data[pixelIndex + 3]; // アルファ値が必要な場合
            }

        return pixelList;
    }

    /// <summary>
    ///     Use the median cut algorithm to cluster similar colors.
    /// </summary>
    /// <param name="pixelArray">Pixel array.</param>
    /// <param name="colorCount">The color count.</param>
    /// <returns></returns>
    private static CMap GetColorMap(byte[][] pixelArray, int colorCount)
    {
        // Send array to quantize function which clusters values using median
        // cut algorithm

        if (colorCount > 0)
        {
            --colorCount;
        }

        var cmap = Mmcq.Quantize(pixelArray, colorCount);
        return cmap;
    }

    private static byte[][] ConvertPixels(byte[] pixels, int pixelCount, int quality, bool ignoreWhite)
    {


        var expectedDataLength = pixelCount * ColorDepth;
        if (expectedDataLength != pixels.Length)
        {
            throw new ArgumentException("(expectedDataLength = "
                                        + expectedDataLength + ") != (pixels.length = "
                                        + pixels.Length + ")");
        }

        // Store the RGB values in an array format suitable for quantize
        // function

        // numRegardedPixels must be rounded up to avoid an
        // ArrayIndexOutOfBoundsException if all pixels are good.

        var numRegardedPixels = (pixelCount + quality - 1) / quality;

        var numUsedPixels = 0;
        var pixelArray = new byte[numRegardedPixels][];

        for (var i = 0; i < pixelCount; i += quality)
        {
            var offset = i * ColorDepth;
            var b = pixels[offset];
            var g = pixels[offset + 1];
            var r = pixels[offset + 2];
            var a = pixels[offset + 3];

            // If pixel is mostly opaque and not white
            if (a >= 125 && !(ignoreWhite && r > 250 && g > 250 && b > 250))
            {
                pixelArray[numUsedPixels] = [r, g, b];
                numUsedPixels++;
            }
        }

        // Remove unused pixels from the array
        var copy = new byte[numUsedPixels][];
        Array.Copy(pixelArray, copy, numUsedPixels);
        return copy;
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