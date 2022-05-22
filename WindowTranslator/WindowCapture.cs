using Composition.WindowsRuntimeHelpers;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Numerics;
using System.Windows;
using System.Windows.Interop;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI.Composition;

namespace WindowTranslator;

public class WindowCapture : FrameworkElement
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
        DependencyProperty.Register(nameof(TargetWindow), typeof(IntPtr), typeof(WindowCapture), new PropertyMetadata(IntPtr.Zero, (d, e) => ((WindowCapture)d).OnTargetWindowChanged()));

    public WindowCapture()
    {
        this.device = Direct3D11Helper.CreateDevice()!;
        this.d3dDevice = Direct3D11Helper.CreateSharpDXDevice(device);

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
        this.swapChain = new SwapChain1(new(), this.d3dDevice, ref description);


        this.compositor = new Compositor();
        this.root = this.compositor.CreateContainerVisual();
        this.root.RelativeSizeAdjustment = Vector2.One;

        this.brush = compositor.CreateSurfaceBrush();
        this.brush.HorizontalAlignmentRatio = 0.5f;
        this.brush.VerticalAlignmentRatio = 0.5f;
        this.brush.Stretch = CompositionStretch.Uniform;

        this.content = compositor.CreateSpriteVisual();
        this.content.AnchorPoint = new Vector2(0.5f);
        this.content.RelativeOffsetAdjustment = new Vector3(0.5f, 0.5f, 0);
        this.content.RelativeSizeAdjustment = Vector2.One;
        this.content.Brush = brush;
        this.root.Children.InsertAtTop(content);


        this.Loaded += WindowCapture_Loaded;
    }

    private void WindowCapture_Loaded(object sender, RoutedEventArgs e)
    {
        this.Loaded -= WindowCapture_Loaded;
        this.Unloaded += WindowCapture_Unloaded;

        var interopWindow = new WindowInteropHelper(Window.GetWindow(this));
        var target = compositor.CreateDesktopWindowTarget(interopWindow.Handle, true);

        target.Root = this.root;
    }

    private void WindowCapture_Unloaded(object sender, RoutedEventArgs e)
    {
        Unload();
    }

    private void OnTargetWindowChanged()
    {
        if (this.TargetWindow == IntPtr.Zero)
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
        this.brush.Surface = null;
        this.session?.Dispose();
        this.framePool?.Dispose();
        this.swapChain?.Dispose();
    }

    private void CreateCaptureVisual()
    {
        var item = CaptureHelper.CreateItemForWindow(this.TargetWindow)!;


        this.framePool = Direct3D11CaptureFramePool.Create(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        this.session = framePool.CreateCaptureSession(item);
        this.session.IsCursorCaptureEnabled = false;
        this.session.IsBorderRequired = false;
        this.lastSize = item.Size;
        this.swapChain.ResizeBuffers(2, lastSize.Width, lastSize.Height, Format.B8G8R8A8_UNorm, SwapChainFlags.None);

        this.framePool.FrameArrived += OnFrameArrived;


        this.session.StartCapture();

        var s = compositor.CreateCompositionSurfaceForSwapChain(this.swapChain);
        this.brush.Surface = s;
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

}
