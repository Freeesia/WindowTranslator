using Composition.WindowsRuntimeHelpers;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Graphics;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowTranslator.Modules.Capture;
using static Windows.Win32.PInvoke;

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

        if (compositionHost.Compositor is { } compositor)
        {
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
        }

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

    public class CompositionHost(double height, double width) : HwndHost
    {
        HWND hwndHost;
        private readonly int hostHeight = (int)height;
        private readonly int hostWidth = (int)width;
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
                if (compositionTarget is not null)
                {
                    compositionTarget.Root = value;
                }
            }
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            hwndHost = CreateWindowEx(0, "static", "",
                WINDOW_STYLE.WS_CHILD | WINDOW_STYLE.WS_VISIBLE,
                0, 0,
                hostWidth, hostHeight,
                (HWND)hwndParent.Handle,
                null!,
                null!,
                []);

            // ほかのコントローラをオーバーレイさせるためにキャプチャーは一番下のレイヤー扱い
            SetWindowPos(hwndHost, new(1), 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);

            // Build Composition Tree of content
            InitComposition(hwndHost);

            return new HandleRef(this, hwndHost);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            compositionTarget?.Root?.Dispose();
            compositionTarget?.Dispose();
            DestroyWindow((HWND)hwnd.Handle);
        }

        private void InitComposition(IntPtr hwndHost)
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                Compositor = new Compositor();
                compositionTarget = Compositor.CreateDesktopWindowTarget(hwndHost, true);
            }
        }
    }
}
