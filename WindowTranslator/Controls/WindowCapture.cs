using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Composition.WindowsRuntimeHelpers;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.Translate;
using BitmapEncoder = Windows.Graphics.Imaging.BitmapEncoder;

namespace WindowTranslator.Controls;

public sealed class WindowCapture : Control, IDisposable
{
    private static readonly DependencyPropertyKey CaptureSourcePropertyKey = DependencyProperty.RegisterReadOnly(nameof(CaptureSource), typeof(ImageSource), typeof(WindowCapture), new PropertyMetadata(null));
    private static readonly DependencyPropertyKey CaptureWidthPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CaptureWidth), typeof(double), typeof(WindowCapture), new PropertyMetadata(1000.0));
    private static readonly DependencyPropertyKey CaptureHeightPropertyKey = DependencyProperty.RegisterReadOnly(nameof(CaptureHeight), typeof(double), typeof(WindowCapture), new PropertyMetadata(1000.0));

    static WindowCapture()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(WindowCapture), new FrameworkPropertyMetadata(typeof(WindowCapture)));
    }

    private readonly IDirect3DDevice device;
    private readonly Direct3D11CaptureFramePool framePool;
    private readonly WindowsMediaOcr ocr = new();
    private readonly DeepLTranslator translator = new();
    private readonly SemaphoreSlim analyzing = new(1, 1);
    private readonly Dictionary<string, string> dic = new();
    private bool isDisposed = false;
    private GraphicsCaptureSession? session;
    private Timer? timer;

    public IntPtr TargetWindow
    {
        get => (IntPtr)GetValue(TargetWindowProperty);
        set => SetValue(TargetWindowProperty, value);
    }

    /// <summary>Identifies the <see cref="TargetWindow"/> dependency property.</summary>
    public static readonly DependencyProperty TargetWindowProperty =
        DependencyProperty.Register(nameof(TargetWindow), typeof(IntPtr), typeof(WindowCapture), new PropertyMetadata(IntPtr.Zero, (d, e) => ((WindowCapture)d).OnTargetWindowChanged()));

    public IEnumerable<TextResult> OcrTexts
    {
        get => (IEnumerable<TextResult>)GetValue(OcrTextsProperty);
        set => SetValue(OcrTextsProperty, value);
    }

    /// <summary>Identifies the <see cref="OcrTexts"/> dependency property.</summary>
    public static readonly DependencyProperty OcrTextsProperty =
        DependencyProperty.Register(nameof(OcrTexts), typeof(IEnumerable<TextResult>), typeof(WindowCapture), new PropertyMetadata(Enumerable.Empty<TextResult>()));

    public double CaptureWidth
    {
        get => (double)GetValue(CaptureWidthProperty);
        set => SetValue(CaptureWidthPropertyKey, value);
    }

    /// <summary>Identifies the <see cref="CaptureWidth"/> dependency property.</summary>
    public static readonly DependencyProperty CaptureWidthProperty = CaptureWidthPropertyKey.DependencyProperty;

    public double CaptureHeight
    {
        get => (double)GetValue(CaptureHeightProperty);
        set => SetValue(CaptureHeightPropertyKey, value);
    }

    /// <summary>Identifies the <see cref="CaptureHeight"/> dependency property.</summary>
    public static readonly DependencyProperty CaptureHeightProperty = CaptureHeightPropertyKey.DependencyProperty;

    public ImageSource? CaptureSource
    {
        get => (ImageSource?)GetValue(CaptureSourceProperty);
        private set => SetValue(CaptureSourcePropertyKey, value);
    }

    /// <summary>Identifies the <see cref="CaptureSource"/> dependency property.</summary>
    public static readonly DependencyProperty CaptureSourceProperty = CaptureSourcePropertyKey.DependencyProperty;

    public WindowCapture()
    {
        device = Direct3D11Helper.CreateDevice()!;
        framePool = Direct3D11CaptureFramePool.Create(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, new((int)this.CaptureWidth, (int)this.CaptureHeight));

        this.Unloaded += (_, _) => Dispose();
        Application.Current.Dispatcher.ShutdownStarted += (_, _) => Dispose();
    }

    public void Dispose()
    {
        StopCapture();
        if (!this.isDisposed)
        {
            this.isDisposed = true;
            this.framePool?.Dispose();
            this.device?.Dispose();
        }
    }

    private void OnTargetWindowChanged()
    {
        if (TargetWindow == IntPtr.Zero)
        {
            StopCapture();
        }
        else
        {
            StartCapture();
        }
    }


    private void StopCapture()
    {
        session?.Dispose();
        session = null;
        this.timer?.Dispose();
    }

    private void StartCapture()
    {
        var item = CaptureHelper.CreateItemForWindow(TargetWindow)!;
        this.CaptureWidth = item.Size.Width;
        this.CaptureHeight = item.Size.Height;
        framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
        session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        session.IsBorderRequired = false;
        session.StartCapture();

        this.timer = new(_ => AnalyzeWindow(), null, 0, 1000);
    }

    private async void AnalyzeWindow()
    {
        var sw = Stopwatch.StartNew();
        if (!this.analyzing.Wait(0))
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
        var width = Math.Clamp(sbmp.PixelWidth, 0, 1270);
        var height = (int)(sbmp.PixelHeight * (1270.0 / sbmp.PixelWidth));
        using (var stream = new InMemoryRandomAccessStream())
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(sbmp);
            if (sbmp.PixelWidth > width)
            {
                encoder.BitmapTransform.ScaledWidth = (uint)width;
                encoder.BitmapTransform.ScaledHeight = (uint)height;
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.NearestNeighbor;
            }

            await encoder.FlushAsync();
            using var bmp = new Bitmap(stream.AsStream());
            this.Dispatcher.Invoke((Bitmap img) => this.CaptureSource = Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height)), bmp);
        }

        _ = this.Dispatcher.BeginInvoke((SizeInt32 lastSize) =>
        {
            if (lastSize.Width != this.CaptureWidth || lastSize.Height != this.CaptureHeight)
            {
                this.CaptureWidth = lastSize.Width;
                this.CaptureHeight = lastSize.Height;
                framePool.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, lastSize);
            }
        }, frame.ContentSize);

        var texts = await this.ocr.RecognizeAsync(sbmp);
        var transTargets = texts.Select(w => w.Text).Where(t => !this.dic.ContainsKey(t)).Distinct().ToArray();
        if (transTargets.Any())
        {
            var translated = await this.translator.TranslateAsync(transTargets);
            foreach (var (src, dst) in transTargets.Zip(translated))
            {
                this.dic.Add(src, dst);
            }
        }
        texts = texts.Select(t => t with { Text = this.dic[t.Text] }).ToArray();
        _ = this.Dispatcher.BeginInvoke(() => SetCurrentValue(OcrTextsProperty, texts));
        Debug.WriteLine($"MAX: {100.0 * texts.Select(t => t.Height).DefaultIfEmpty().Max() / sbmp.PixelHeight}%, Time: {sw.Elapsed}");
    }
}
