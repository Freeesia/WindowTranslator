using Microsoft.VisualStudio.Threading;
using Windows.Graphics.Capture;

namespace WindowTranslator.Modules.Capture;
public interface ICaptureModule
{
    event AsyncEventHandler<CapturedEventArgs>? Captured;
    event AsyncEventHandler? CaptureStarted;
    void StartCapture(IntPtr targetWindow);
    void StopCapture();
}

public record CapturedEventArgs(Direct3D11CaptureFrame Frame);
