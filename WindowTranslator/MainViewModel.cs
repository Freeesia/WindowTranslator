using CommunityToolkit.Mvvm.ComponentModel;
using Composition.WindowsRuntimeHelpers;
using System.Diagnostics;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Timer = System.Timers.Timer;

namespace WindowTranslator;

[ObservableObject]
public sealed partial class MainViewModel : IDisposable
{
    private readonly Timer timer = new(1000);
    private readonly IDirect3DDevice device;
    private readonly Direct3D11CaptureFramePool framePool;
    private readonly GraphicsCaptureSession session;
    private SizeInt32 lastSize;

    public IntPtr WindowHandle { get; }

    [ObservableProperty]
    private string ocrText = string.Empty;

    public MainViewModel(IntPtr windowHandle)
    {
        this.WindowHandle = windowHandle;
        //var item = CaptureHelper.CreateItemForWindow(this.WindowHandle)!;
        //this.framePool = Direct3D11CaptureFramePool.Create(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        //this.session = framePool.CreateCaptureSession(item);
        //this.session.IsCursorCaptureEnabled = false;
        //this.session.IsBorderRequired = false;
        //this.lastSize = item.Size;
        //this.session.StartCapture();

        //this.timer.Elapsed += (_, _) => AnalyzeWindow();
        //this.timer.Start();
    }

    public void Dispose()
    {
        //this.session.Dispose();
        //this.framePool.Dispose();
        //this.device.Dispose();
        //this.timer.Dispose();
    }

    private async void AnalyzeWindow()
    {
        var sw = Stopwatch.StartNew();

        using var frame = this.framePool.TryGetNextFrame();
        if (frame is null)
        {
            return;
        }
        var sbmp = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
        if (frame.ContentSize.Width != lastSize.Width || frame.ContentSize.Height != lastSize.Height)
        {
            lastSize = frame.ContentSize;
            this.framePool.Recreate(this.device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, lastSize);
        }

        var ocr = OcrEngine.TryCreateFromLanguage(new("ja-JP"));
        var result = await ocr.RecognizeAsync(sbmp);
        this.OcrText = result.Text;
        Debug.WriteLine(result.Text);
        Debug.WriteLine(sw.Elapsed);
    }
}
