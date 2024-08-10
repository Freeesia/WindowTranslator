using Microsoft.Windows.Imaging;
using Microsoft.Windows.Vision;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Modules.Ocr;
internal class WindowsAppOcr : IOcrModule
{
    public ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        await TextRecognizer.MakeAvailableAsync();
        var textRecognizer = await TextRecognizer.CreateAsync();

        var options = new TextRecognizerOptions();

        // create ImageBuffer from image
        var imageBuffer = ImageBuffer.CreateBufferAttachedToBitmap(image);

        var recognizedText = await textRecognizer.RecognizeTextFromImageAsync(imageBuffer, options);
    }
}
