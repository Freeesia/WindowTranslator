using CommunityToolkit.Mvvm.ComponentModel;
using Kamishibai;
using Microsoft.VisualStudio.Threading;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WindowTranslator.Modules.Cache;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Modules.Translate;
using WindowTranslator.Stores;
using BitmapEncoder = Windows.Graphics.Imaging.BitmapEncoder;

namespace WindowTranslator;

[OpenWindow]
[ObservableObject]
public sealed partial class MainViewModel
{
    private readonly Dispatcher dispatcher;
    private readonly IProcessInfoStore processInfoStore;
    private readonly ICaptureModule capture;
    private readonly IOcrModule ocr;
    private readonly ITranslateModule translator;
    private readonly ICacheModule cache;
    private readonly IColorModule color;
    [ObservableProperty]
    private IEnumerable<TextRect> ocrTexts = Enumerable.Empty<TextRect>();

    [ObservableProperty]
    private double captureWidth = 1000;

    [ObservableProperty]
    private double captureHeight = 1000;

    [ObservableProperty]
    private BitmapSource? captureSource;

    public MainViewModel([Inject] IProcessInfoStore processInfoStore, [Inject] ICaptureModule capture, [Inject] IOcrModule ocr, [Inject] ITranslateModule translator, [Inject] ICacheModule cache, [Inject] IColorModule color)
    {
        this.dispatcher = Dispatcher.CurrentDispatcher;
        this.processInfoStore = processInfoStore;
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.capture.Captured += Capture_CapturedAsync;
        this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.color = color ?? throw new ArgumentNullException(nameof(color));
        this.capture.StartCapture(this.processInfoStore.MainWindowHangle);
    }

    private Task Capture_CapturedAsync(object? sender, CapturedEventArgs args)
    {
        var sbmp = args.Capture;
        return Task.WhenAll(CreateTextOverlayAsync(sbmp), CreateImageAsync(sbmp));
    }

    private async Task CreateTextOverlayAsync(SoftwareBitmap sbmp)
    {
        var texts = await this.ocr.RecognizeAsync(sbmp);
        await Task.WhenAll(TranslateAsync(texts), Task.Run(async () =>
        {
            texts = await this.color.ConvertColor(sbmp, texts);
        }));
        this.OcrTexts = texts.Select(t => t with { Text = this.cache.Get(t.Text) }).ToArray();
    }

    private async Task TranslateAsync(IEnumerable<TextRect> texts)
    {
        var transTargets = texts.Select(w => w.Text).Distinct().Where(t => !this.cache.Contains(t)).ToArray();
        if (transTargets.Any())
        {
            var translated = await this.translator.TranslateAsync(transTargets);
            this.cache.AddRange(transTargets.Zip(translated));
        }
    }

    private async Task CreateImageAsync(SoftwareBitmap sbmp)
    {
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
            await this.dispatcher.InvokeAsync(() =>
            {
                // メモリリークしてる
                this.CaptureSource = Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(bmp.Width, bmp.Height));
                this.CaptureSource?.Freeze();
            });
        }

        if (sbmp.PixelWidth != this.CaptureWidth || sbmp.PixelHeight != this.CaptureHeight)
        {
            this.CaptureWidth = sbmp.PixelWidth;
            this.CaptureHeight = sbmp.PixelHeight;
        }
    }
}
