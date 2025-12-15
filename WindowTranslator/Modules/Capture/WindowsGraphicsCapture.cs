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
using WindowTranslator.Stores;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Modules.Capture;

[DefaultModule]
[DisplayName("Windows標準キャプチャー")]
public sealed class WindowsGraphicsCapture(ILogger<WindowsGraphicsCapture> logger, IProcessInfoStore processInfo) : ICaptureModule, IDisposable
{
    private readonly IDirect3DDevice device = Direct3D11Helper.GetOrCreateDevice()!;
    private readonly SemaphoreSlim processing = new(1, 1);
    private readonly ILogger<WindowsGraphicsCapture> logger = logger;
    private readonly IProcessInfoStore processInfo = processInfo;
    private readonly CancellationTokenSource cts = new();
    private nint targetWindow;
    private bool isMonitor;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private SizeInt32 lastSize = new(1000, 1000);
    private bool lastMaximized;

    public event AsyncEventHandler<CapturedEventArgs>? Captured;

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
        this.targetWindow = targetWindow;
        
        // ディスプレイかウィンドウかを判定
        this.isMonitor = this.processInfo.Name.StartsWith("DISPLAY__", StringComparison.OrdinalIgnoreCase);
        
        GraphicsCaptureItem? item;
        if (this.isMonitor)
        {
            this.logger.LogDebug("Creating capture item for monitor");
            item = CaptureHelper.CreateItemForMonitor(targetWindow);
        }
        else
        {
            this.logger.LogDebug("Creating capture item for window");
            item = CaptureHelper.CreateItemForWindow(targetWindow);
        }
        
        if (item is null)
        {
            throw new InvalidOperationException("Failed to create capture item");
        }
        
        this.lastSize = item.Size;
        this.lastMaximized = this.isMonitor ? false : IsZoomed(new(targetWindow));
        this.framePool = Direct3D11CaptureFramePool.Create(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
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
        try
        {
            this.logger.LogDebug("FramePool_FrameArrived");
            await this.processing.WaitAsync();
            using var rel = new DisposeAction(() => this.processing.Release());
            this.logger.LogDebug("TryGetNextFrame");
            using var frame = sender.TryGetNextFrame();
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
            // モニターの場合は最大化チェックをスキップ
            if (!this.isMonitor && this.lastMaximized != IsZoomed(new(targetWindow)))
            {
                this.lastMaximized = !this.lastMaximized;
                this.logger.LogDebug("セッション再作成");
                StopCapture();
                StartCapture(targetWindow);
                return;
            }
            this.cts.Token.ThrowIfCancellationRequested();
            this.lastSize = frame.ContentSize;
            this.logger.LogDebug($"フレームプール再生成:({this.lastSize.Width}, {this.lastSize.Height})");
            sender.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
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
        this.session?.Dispose();
        this.framePool?.Dispose();
    }
}
