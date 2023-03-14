using CommunityToolkit.Mvvm.ComponentModel;
using Kamishibai;
using Microsoft.VisualStudio.Threading;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Main;

[OpenWindow]
[ObservableObject]
public sealed partial class MainViewModel : IDisposable
{
    private readonly Timer timer;
    private readonly IOcrModule ocr;
    private readonly ITranslateModule translator;
    private readonly ICacheModule cache;
    private readonly IColorModule color;
    private readonly SemaphoreSlim analyzing = new(1, 1);
    [ObservableProperty]
    private IEnumerable<TextRect> ocrTexts = Enumerable.Empty<TextRect>();

    private SoftwareBitmap? sbmp;

    public ICaptureModule Capture { get; }

    public IProcessInfoStore TargetProcess { get; }

    public MainViewModel([Inject] IProcessInfoStore processInfoStore, [Inject] ICaptureModule capture, [Inject] IOcrModule ocr, [Inject] ITranslateModule translator, [Inject] ICacheModule cache, [Inject] IColorModule color)
    {
        this.TargetProcess = processInfoStore;
        this.Capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.Capture.Captured += Capture_CapturedAsync;
        this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.color = color ?? throw new ArgumentNullException(nameof(color));
        this.Capture.StartCapture(this.TargetProcess.MainWindowHangle);
        this.timer = new(_ => CreateTextOverlayAsync().Forget(), null, 0, 500);
    }

    public void Dispose()
        => this.timer?.Dispose();

    private async Task Capture_CapturedAsync(object? sender, CapturedEventArgs args)
    {
        var newBmp = await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface);
        var sbmp = Interlocked.Exchange(ref this.sbmp, newBmp);
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
