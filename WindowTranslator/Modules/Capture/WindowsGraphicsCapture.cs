using Composition.WindowsRuntimeHelpers;
using Microsoft.VisualStudio.Threading;
using System.ComponentModel;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace WindowTranslator.Modules.Capture;

[DefaultModule]
[DisplayName("Windows標準キャプチャー")]
public sealed partial class WindowsGraphicsCapture : ICaptureModule, IDisposable
{
    private readonly Direct3D11CaptureFramePool framePool;
    private readonly IDirect3DDevice device;
    private readonly SemaphoreSlim processing = new(1, 1);
    private GraphicsCaptureSession? session;
    private SizeInt32 lastSize = new(1000, 1000);

    public event AsyncEventHandler<CapturedEventArgs>? Captured;

    public WindowsGraphicsCapture()
    {
        this.device = Direct3D11Helper.CreateDevice()!;
        this.framePool = Direct3D11CaptureFramePool.Create(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
    }


    public void Dispose()
    {
        this.session?.Dispose();
        this.framePool?.Dispose();
        this.device?.Dispose();
    }

    public void StartCapture(IntPtr targetWindow)
    {
        var item = CaptureHelper.CreateItemForWindow(targetWindow)!;
        this.lastSize = item.Size;
        this.framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
        this.framePool.FrameArrived += FramePool_FrameArrived;
        this.session = this.framePool.CreateCaptureSession(item);
        this.session.IsCursorCaptureEnabled = false;
        if (ApiInformation.IsWriteablePropertyPresent(typeof(GraphicsCaptureSession).FullName, nameof(GraphicsCaptureSession.IsBorderRequired)))
        {
            this.session.IsBorderRequired = false;
        }
        this.session.StartCapture();
    }

    private async void FramePool_FrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        if (!await this.processing.WaitAsync(0))
        {
            return;
        }
        using var rel = new DisposeAction(() => this.processing.Release());
        using var frame = framePool.TryGetNextFrame();
        if (frame is null)
        {
            return;
        }

        if (this.Captured is { } handler)
        {
            await handler.InvokeAsync(this, new(frame));
        }

        if (lastSize.Width != frame.ContentSize.Width || lastSize.Height != frame.ContentSize.Height)
        {
            this.lastSize = frame.ContentSize;
            framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
        }
    }

    public void StopCapture()
    {
        throw new NotImplementedException();
    }
}
