using Microsoft.VisualStudio.Threading;
using Windows.Graphics.Imaging;

namespace WindowTranslator.Modules.Capture;
public interface ICaptureModule
{
    event AsyncEventHandler<CapturedEventArgs>? Captured;
    void StartCapture(IntPtr targetWindow);
    void StopCapture();
}

public record CapturedEventArgs(SoftwareBitmap Capture);