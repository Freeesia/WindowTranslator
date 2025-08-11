using Composition.WindowsRuntimeHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using System.ComponentModel;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Modules.Capture;

[DefaultModule]
[DisplayName("Windows標準キャプチャー")]
public sealed partial class WindowsGraphicsCapture : ICaptureModule, IDisposable
{
    private readonly Direct3D11CaptureFramePool framePool;
    private readonly IDirect3DDevice device;
    private readonly SemaphoreSlim processing = new(1, 1);
    private readonly ILogger<WindowsGraphicsCapture> logger;
    private readonly CancellationTokenSource cts = new();
    private GraphicsCaptureSession? session;
    private SizeInt32 lastSize = new(1000, 1000);
    private bool hasBeenStopped = false;

    public event AsyncEventHandler<CapturedEventArgs>? Captured;
    public event AsyncEventHandler? CaptureStarted;

    public WindowsGraphicsCapture(ILogger<WindowsGraphicsCapture> logger)
    {
        this.device = Direct3D11Helper.GetOrCreateDevice()!;
        this.framePool = Direct3D11CaptureFramePool.Create(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
        this.logger = logger;
    }


    public void Dispose()
    {
        this.logger.LogDebug("Dispose");
        this.cts.Cancel();
        this.session?.Dispose();
        this.framePool?.Dispose();
    }

    public void StartCapture(IntPtr targetWindow)
    {
        this.logger.LogDebug("StartCapture");
        var item = CaptureHelper.CreateItemForWindow(targetWindow)!;
        this.lastSize = item.Size;
        this.framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, this.lastSize);
        this.framePool.FrameArrived += FramePool_FrameArrived;
        this.session = this.framePool.CreateCaptureSession(item);
        this.session.IsCursorCaptureEnabled = false;
        if (ApiInformation.IsWriteablePropertyPresent(typeof(GraphicsCaptureSession).FullName, nameof(GraphicsCaptureSession.IsBorderRequired)))
        {
            this.session.IsBorderRequired = false;
        }
        this.session.StartCapture();
        
        // Fire CaptureStarted event if this is a restart (after StopCapture was called)
        if (this.hasBeenStopped && this.CaptureStarted is { } handler)
        {
            this.logger.LogDebug("Firing CaptureStarted event (restart detected)");
            handler.InvokeAsync(this, new()).Forget();
        }
    }

    private async void FramePool_FrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            this.logger.LogDebug("FramePool_FrameArrived");
            await this.processing.WaitAsync();
            using var rel = new DisposeAction(() => this.processing.Release());
            this.logger.LogDebug("TryGetNextFrame");
            using var frame = framePool.TryGetNextFrame();
            if (frame is null)
            {
                return;
            }
            this.logger.LogDebug($"フレーム取得完了:({frame.ContentSize.Width}, {frame.ContentSize.Height})");
            if (this.Captured is { } handler)
            {
                await handler.InvokeAsync(this, new(frame));
            }
            this.cts.Token.ThrowIfCancellationRequested();
            this.logger.LogDebug("イベント完了");
            if (lastSize.Width == frame.ContentSize.Width && lastSize.Height == frame.ContentSize.Height)
            {
                return;
            }
            this.lastSize = frame.ContentSize;
            this.logger.LogDebug($"フレームプール再生成:({this.lastSize.Width}, {this.lastSize.Height})");
            this.cts.Token.ThrowIfCancellationRequested();
            framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
            this.logger.LogDebug($"フレームプール再生成後:({this.lastSize.Width}, {this.lastSize.Height})");
        }
        catch (ObjectDisposedException)
        {
            this.logger.LogDebug($"破棄済み");
        }
        catch (OperationCanceledException)
        {
            this.logger.LogDebug($"キャンセル処理");
        }
    }

    public void StopCapture()
    {
        this.logger.LogDebug("StopCapture");
        this.hasBeenStopped = true;
        this.session?.Dispose();
        this.framePool?.Dispose();
    }
}
