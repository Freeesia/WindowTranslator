using Composition.WindowsRuntimeHelpers;
using PInvoke;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;

namespace WindowTranslator.Controls;

public class WindowCaptureCompositionhost : FrameworkElement
{
    private readonly IDirect3DDevice device;
    private readonly SharpDX.Direct3D11.Device d3dDevice;
    private readonly SwapChain1 swapChain;
    private readonly Compositor compositor;
    private readonly ContainerVisual root;
    private readonly CompositionSurfaceBrush brush;
    private readonly SpriteVisual content;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private SizeInt32 lastSize;

    public IntPtr TargetWindow
    {
        get => (IntPtr)GetValue(TargetWindowProperty);
        set => SetValue(TargetWindowProperty, value);
    }

    /// <summary>Identifies the <see cref="TargetWindow"/> dependency property.</summary>
    public static readonly DependencyProperty TargetWindowProperty =
        DependencyProperty.Register(nameof(TargetWindow), typeof(IntPtr), typeof(WindowCaptureCompositionhost), new PropertyMetadata(IntPtr.Zero, (d, e) => ((WindowCaptureCompositionhost)d).OnTargetWindowChanged()));

    public WindowCaptureCompositionhost()
    {
        device = Direct3D11Helper.CreateDevice()!;
        d3dDevice = Direct3D11Helper.CreateSharpDXDevice(device);

        var description = new SwapChainDescription1()
        {
            // 後でちゃんとした値入れる
            Width = 1000,
            Height = 1000,
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


        compositor = new Compositor();
        root = compositor.CreateContainerVisual();
        root.RelativeSizeAdjustment = Vector2.One;

        brush = compositor.CreateSurfaceBrush();
        brush.HorizontalAlignmentRatio = 0.5f;
        brush.VerticalAlignmentRatio = 0.5f;
        brush.Stretch = CompositionStretch.Uniform;

        content = compositor.CreateSpriteVisual();
        content.AnchorPoint = new Vector2(0.5f);
        content.RelativeOffsetAdjustment = new Vector3(0.5f, 0.5f, 0);
        content.RelativeSizeAdjustment = Vector2.One;
        content.Brush = brush;
        root.Children.InsertAtTop(content);


        Loaded += WindowCapture_Loaded;
    }

    private void WindowCapture_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= WindowCapture_Loaded;
        Unloaded += WindowCapture_Unloaded;

        var interopWindow = new WindowInteropHelper(Window.GetWindow(this));
        var target = compositor.CreateDesktopWindowTarget(interopWindow.Handle, true);

        target.Root = root;
    }

    private void WindowCapture_Unloaded(object sender, RoutedEventArgs e)
    {
        Unload();
    }

    private void OnTargetWindowChanged()
    {
        if (TargetWindow == IntPtr.Zero)
        {
            Unload();
        }
        else
        {
            CreateCaptureVisual();
        }
    }

    private void Unload()
    {
        brush.Surface = null;
        session?.Dispose();
        framePool?.Dispose();
        swapChain?.Dispose();
    }

    private void CreateCaptureVisual()
    {
        var item = CaptureHelper.CreateItemForWindow(TargetWindow)!;


        framePool = Direct3D11CaptureFramePool.Create(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        session.IsBorderRequired = false;
        lastSize = item.Size;
        swapChain.ResizeBuffers(2, lastSize.Width, lastSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);

        framePool.FrameArrived += OnFrameArrived;


        session.StartCapture();

        var s = compositor.CreateCompositionSurfaceForSwapChain(swapChain);
        brush.Surface = s;
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        var newSize = false;

        using (var frame = sender.TryGetNextFrame())
        {
            if (frame.ContentSize.Width != lastSize.Width || frame.ContentSize.Height != lastSize.Height)
            {
                newSize = true;
                lastSize = frame.ContentSize;
                swapChain.ResizeBuffers(2, lastSize.Width, lastSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);
            }

            using var backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
            using var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface);
            d3dDevice.ImmediateContext.CopyResource(bitmap, backBuffer);
        } // Retire the frame.

        swapChain.Present(0, PresentFlags.None);

        if (newSize)
        {
            sender.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, lastSize);
        }
    }

    private class CompositionHost : HwndHost
    {
        IntPtr hwndHost;
        int hostHeight, hostWidth;
        CompositionTarget compositionTarget;

        public Compositor Compositor { get; private set; }

        public Visual Child
        {
            set
            {
                if (Compositor == null)
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

            // Build Composition Tree of content
            InitComposition(hwndHost);

            return new HandleRef(this, hwndHost);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            compositionTarget.Root?.Dispose();
            User32.DestroyWindow(hwnd.Handle);
        }

        private void InitComposition(IntPtr hwndHost)
        {
            Compositor = new Compositor();
            compositionTarget = Compositor.CreateDesktopWindowTarget(hwndHost, true);
        }
    }
}
