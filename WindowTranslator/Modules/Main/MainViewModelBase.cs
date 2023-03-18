using CommunityToolkit.Mvvm.ComponentModel;
using Kamishibai;
using Microsoft.VisualStudio.Threading;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Main;

[ObservableObject]
public abstract partial class MainViewModelBase : IDisposable
{
    private readonly Timer timer;
    private readonly IOcrModule ocr;
    private readonly ITranslateModule translator;
    private readonly ICacheModule cache;
    private readonly IColorModule color;
    private readonly SemaphoreSlim analyzing = new(1, 1);
    private readonly ICaptureModule capture;
    [ObservableProperty]
    private IEnumerable<TextRect> ocrTexts = Enumerable.Empty<TextRect>();

    [ObservableProperty]
    private double width = double.NaN;
    [ObservableProperty]
    private double height = double.NaN;

    private SoftwareBitmap? sbmp;

    public MainViewModelBase(IProcessInfoStore processInfoStore, ICaptureModule capture, IOcrModule ocr, ITranslateModule translator, ICacheModule cache, IColorModule color)
    {
        var targetProcess = processInfoStore;
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.capture.Captured += Capture_CapturedAsync;
        this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.color = color ?? throw new ArgumentNullException(nameof(color));
        this.capture.StartCapture(targetProcess.MainWindowHangle);
        this.timer = new(_ => CreateTextOverlayAsync().Forget(), null, 0, 500);
    }

    public void Dispose()
        => this.timer?.Dispose();

    private async Task Capture_CapturedAsync(object? sender, CapturedEventArgs args)
    {
        var newBmp = await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface);
        var sbmp = Interlocked.Exchange(ref this.sbmp, newBmp);
        this.Width = newBmp.PixelWidth;
        this.Height = newBmp.PixelHeight;
        sbmp?.Dispose();
    }

    private async Task CreateTextOverlayAsync()
    {
        if (!await this.analyzing.WaitAsync(0))
        {
            return;
        }
        using var rel = new DisposeAction(() => this.analyzing.Release());
        using var sbmp = Interlocked.Exchange(ref this.sbmp, null);
        if (sbmp is null)
        {
            return;
        }
        var texts = await this.ocr.RecognizeAsync(sbmp);
        await Task.WhenAll(TranslateAsync(texts), Task.Run(async () =>
        {
            texts = await this.color.ConvertColorAsync(sbmp, texts);
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
}

[OpenWindow]
public sealed class CaptureMainViewModel : MainViewModelBase
{
    public ICaptureModule Capture { get; }

    public CaptureMainViewModel([Inject] IProcessInfoStore processInfoStore, [Inject] ICaptureModule capture, [Inject] IOcrModule ocr, [Inject] ITranslateModule translator, [Inject] ICacheModule cache, [Inject] IColorModule color)
        : base(processInfoStore, capture, ocr, translator, cache, color)
    {
        this.Capture = capture ?? throw new ArgumentNullException(nameof(capture));
    }
}

[OpenWindow]
public sealed class OverlayMainViewModel : MainViewModelBase
{
    public OverlayMainViewModel([Inject] IProcessInfoStore processInfoStore, [Inject] ICaptureModule capture, [Inject] IOcrModule ocr, [Inject] ITranslateModule translator, [Inject] ICacheModule cache, [Inject] IColorModule color)
        : base(processInfoStore, capture, ocr, translator, cache, color)
    {
    }
}
