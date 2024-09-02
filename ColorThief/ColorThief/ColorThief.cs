using System.Drawing;

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
    public static List<QuantizedColor> GetPalette(Bitmap sourceImage, int colorCount = DefaultColorCount, int quality = DefaultQuality, bool ignoreWhite = DefaultIgnoreWhite)
    {
        var pixelArray = GetPixelsFast(sourceImage, quality, ignoreWhite);
        var cmap = GetColorMap(pixelArray, colorCount);
        if (cmap != null)
        {
            var colors = cmap.GeneratePalette();
            return colors;
        }
        return [];
    }

    private static byte[][] GetPixelsFast(Bitmap sourceImage, int quality, bool ignoreWhite)
    {
        if (quality < 1)
        {
            quality = DefaultQuality;
        }

        var pixels = GetIntFromPixel(sourceImage);
        var pixelCount = sourceImage.Width * sourceImage.Height;

        return ConvertPixels(pixels, pixelCount, quality, ignoreWhite);
    }

    private static byte[] GetIntFromPixel(Bitmap bmp)
    {
        var pixelList = new byte[bmp.Width * bmp.Height * 4];
        int count = 0;

        for (var x = 0; x < bmp.Width; x++)
        {
            for (var y = 0; y < bmp.Height; y++)
            {
                var clr = bmp.GetPixel(x, y);

                pixelList[count] = clr.B;
                count++;

                pixelList[count] = clr.G;
                count++;

                pixelList[count] = clr.R;
                count++;

                pixelList[count] = clr.A;
                count++;
            }
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
                pixelArray[numUsedPixels] = new[] { r, g, b };
                numUsedPixels++;
            }
        }

        // Remove unused pixels from the array
        var copy = new byte[numUsedPixels][];
        Array.Copy(pixelArray, copy, numUsedPixels);
        return copy;
    }
}
