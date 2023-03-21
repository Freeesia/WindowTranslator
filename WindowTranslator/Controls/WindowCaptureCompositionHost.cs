using Composition.WindowsRuntimeHelpers;
using PInvoke;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Graphics;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;
using WindowTranslator.Modules.Capture;

namespace WindowTranslator.Controls;

public class WindowCaptureCompositionHost : HwndExtensions.Host.HwndHostPresenter
{
    private readonly IDirect3DDevice device;
    private readonly SharpDX.Direct3D11.Device d3dDevice;
    private readonly SwapChain1 swapChain;
    private CompositionHost? compositionHost;
    // 後でちゃんとした値入れる
    private SizeInt32 lastSize = new(1000, 1000);

    public ICaptureModule? CaptureModule
    {
        get => (ICaptureModule?)GetValue(CaptureModuleProperty);
        set => SetValue(CaptureModuleProperty, value);
    }

    /// <summary>Identifies the <see cref="CaptureModule"/> dependency property.</summary>
    public static readonly DependencyProperty CaptureModuleProperty =
        DependencyProperty.Register(
            nameof(CaptureModule),
            typeof(ICaptureModule),
            typeof(WindowCaptureCompositionHost),
            new PropertyMetadata(null, (d, e) => ((WindowCaptureCompositionHost)d).OnCaptureModuleChanged((ICaptureModule?)e.OldValue, (ICaptureModule?)e.NewValue)));

    public WindowCaptureCompositionHost()
    {
        device = Direct3D11Helper.GetOrCreateDevice()!;
        d3dDevice = Direct3D11Helper.CreateSharpDXDevice(device);

        var description = new SwapChainDescription1()
        {
            Width = lastSize.Width,
            Height = lastSize.Height,
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new() { Count = 1, Quality = 0 },
            Usage = Usage.RenderTargetOutput,
            BufferCount = 2,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,
            AlphaMode = AlphaMode.Premultiplied,
            Flags = SwapChainFlags.None
        };
        swapChain = new SwapChain1(new(), d3dDevice, ref description);
        this.Loaded += WindowCaptureCompositionhost_Loaded;
        this.Unloaded += WindowCaptureCompositionhost_Unloaded;
    }

    private void WindowCaptureCompositionhost_Loaded(object sender, RoutedEventArgs e)
    {
        compositionHost = new(lastSize.Height, lastSize.Width);
        this.HwndHost = compositionHost;

        var compositor = compositionHost.Compositor ?? throw new InvalidOperationException();

        var brush = compositor.CreateSurfaceBrush();
        brush.HorizontalAlignmentRatio = 0.5f;
        brush.VerticalAlignmentRatio = 0.5f;
        brush.Stretch = CompositionStretch.Uniform;
        brush.Surface = compositionHost.Compositor?.CreateCompositionSurfaceForSwapChain(swapChain);

        var content = compositor.CreateSpriteVisual();
        content.AnchorPoint = new Vector2(0.5f);
        content.RelativeOffsetAdjustment = new Vector3(0.5f, 0.5f, 0);
        content.RelativeSizeAdjustment = Vector2.One;
        content.Brush = brush;

        compositionHost.Child = content;

        InvalidateMeasure();
    }

    private void WindowCaptureCompositionhost_Unloaded(object sender, RoutedEventArgs e)
    {
        swapChain?.Dispose();
    }

    private void OnCaptureModuleChanged(ICaptureModule? oldValue, ICaptureModule? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Captured -= CaptureModule_CapturedAsync;
        }
        if (newValue is not null)
        {
            newValue.Captured += CaptureModule_CapturedAsync;
        }
    }

    private Task CaptureModule_CapturedAsync(object? sender, CapturedEventArgs args)
    {
        var frame = args.Frame;
        if (frame.ContentSize.Width != lastSize.Width || frame.ContentSize.Height != lastSize.Height)
        {
            lastSize = frame.ContentSize;
            swapChain.ResizeBuffers(2, lastSize.Width, lastSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
            InvalidateMeasure();
        }

        using var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
        using var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
        d3dDevice.ImmediateContext.CopyResource(bitmap, backBuffer);

        swapChain.Present(0, PresentFlags.None);
        return Task.CompletedTask;
    }

    protected override Size MeasureOverride(Size constraint) => new(this.lastSize.Width, this.lastSize.Height);

    public class CompositionHost : HwndHost
    {
        IntPtr hwndHost;
        int hostHeight, hostWidth;
        CompositionTarget? compositionTarget;

        public Compositor? Compositor { get; private set; }

        public Visual Child
        {
            set
            {
                if (compositionTarget is null)
                {
                    InitComposition(hwndHost);
                }
                compositionTarget.Root = value;
            }
        }

        public CompositionHost(double height, double width)
        {
            hostHeight = (int)height;
            hostWidth = (int)width;
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            hwndHost = User32.CreateWindowEx(0, "static", "",
                User32.WindowStyles.WS_CHILD | User32.WindowStyles.WS_VISIBLE,
                0, 0,
                hostWidth, hostHeight,
                hwndParent.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            // ほかのコントローラをオーバーレイさせるためにキャプチャーは一番下のレイヤー扱い
            User32.SetWindowPos(hwndHost, (IntPtr)1, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);

            // Build Composition Tree of content
            InitComposition(hwndHost);

            return new HandleRef(this, hwndHost);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            compositionTarget?.Root?.Dispose();
            compositionTarget?.Dispose();
            User32.DestroyWindow(hwnd.Handle);
        }

        [MemberNotNull(nameof(compositionTarget), nameof(Compositor))]
        private void InitComposition(IntPtr hwndHost)
        {
            Compositor = new Compositor();
            compositionTarget = Compositor.CreateDesktopWindowTarget(hwndHost, true);
        }
    }
}
