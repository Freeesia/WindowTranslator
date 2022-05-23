using CommunityToolkit.Mvvm.ComponentModel;
using Composition.WindowsRuntimeHelpers;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace WindowTranslator.Modules.Capture;

public sealed partial class WindowsGraphicsCapture : ICaptureModule, IDisposable
{
    private readonly Direct3D11CaptureFramePool framePool;
    private readonly IDirect3DDevice device;
    private readonly SemaphoreSlim analyzing = new(1, 1);
    private GraphicsCaptureSession? session;
    private Timer? timer;
    private SizeInt32 lastSize = new(1000, 1000);

    public event AsyncEventHandler<CapturedEventArgs>? Captured;

    public WindowsGraphicsCapture()
    {
        this.device = Direct3D11Helper.CreateDevice()!;
        this.framePool = Direct3D11CaptureFramePool.Create(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
    }


    public void Dispose()
    {
        this.framePool?.Dispose();
        this.device?.Dispose();
    }
    public void StartCapture(IntPtr targetWindow)
    {
        var item = CaptureHelper.CreateItemForWindow(targetWindow)!;
        this.lastSize = item.Size;
        this.framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
        this.session = this.framePool.CreateCaptureSession(item);
        this.session.IsCursorCaptureEnabled = false;
        this.session.IsBorderRequired = false;
        this.session.StartCapture();

        this.timer = new(_ => _ = AnalyzeWindowAsync(), null, 0, 1000);
    }

    public void StopCapture()
    {
        throw new NotImplementedException();
    }

    private async Task AnalyzeWindowAsync()
    {
        var sw = Stopwatch.StartNew();
        if (!await this.analyzing.WaitAsync(0))
        {
            return;
        }
        using var rel = new DisposeAction(() => this.analyzing.Release());
        using var frame = framePool.TryGetNextFrame();
        if (frame is null)
        {
            return;
        }
        using var sbmp = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);

        if (this.Captured is { } handler)
        {
            await handler.InvokeAsync(this, new(sbmp));
        }

        if (lastSize.Width != frame.ContentSize.Width || lastSize.Height != frame.ContentSize.Height)
        {
            this.lastSize = frame.ContentSize;
            framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
        }
    }
}
