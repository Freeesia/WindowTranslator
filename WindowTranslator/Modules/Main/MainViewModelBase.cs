using System.Collections.Concurrent;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Kamishibai;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using Windows.Graphics.Imaging;
using WindowTranslator.ComponentModel;
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
    private readonly IEnumerable<IFilterModule> filters;
    private readonly ILogger logger;
    private readonly SemaphoreSlim analyzing = new(1, 1);
    private readonly IPresentationService presentationService;
    private readonly ICaptureModule capture;
    private readonly ConcurrentDictionary<string, string> requesting = new();
    [ObservableProperty]
    private IEnumerable<TextRect> ocrTexts = [];

    [ObservableProperty]
    private double width = double.NaN;
    [ObservableProperty]
    private double height = double.NaN;

    [ObservableProperty]
    private bool isFirstBusy = true;

    private SoftwareBitmap? sbmp;
    private bool disposedValue;

    public MainViewModelBase(
        IPresentationService presentationService,
        IProcessInfoStore processInfoStore,
        ICaptureModule capture,
        IOcrModule ocr,
        ITranslateModule translator,
        ICacheModule cache,
        IColorModule color,
        IEnumerable<IFilterModule> filters,
        ILogger logger)
    {
        var targetProcess = processInfoStore;
        this.presentationService = presentationService;
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.capture.Captured += Capture_CapturedAsync;
        this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.color = color ?? throw new ArgumentNullException(nameof(color));
        this.filters = filters;
        this.logger = logger;
        this.capture.StartCapture(targetProcess.MainWindowHangle);
        this.timer = new(_ => CreateTextOverlayAsync().Forget(), null, 0, 500);
    }

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
        var sw = Stopwatch.StartNew();
        if (!await this.analyzing.WaitAsync(0))
        {
            return;
        }
        using var rel = new DisposeAction(() =>
        {
            this.analyzing.Release();
            this.IsFirstBusy = false;
        });
        using var sbmp = Interlocked.Exchange(ref this.sbmp, null);
        if (sbmp is null)
        {
            return;
        }
        var texts = await this.ocr.RecognizeAsync(sbmp);
        {
            var tmp = texts.ToAsyncEnumerable();
            foreach (var filter in this.filters)
            {
                tmp = filter.ExecutePreTranslate(tmp);
            }
            texts = await tmp.ToArrayAsync();
        }
        TranslateAsync(texts).Forget();
        texts = await this.color.ConvertColorAsync(sbmp, texts);
        texts = texts.Select(t => t with { IsTranslated = this.cache.Contains(t.Text), Text = this.cache.Get(t.Text) }).ToArray();
        {
            var tmp = texts.ToAsyncEnumerable();
            foreach (var filter in this.filters)
            {
                tmp = filter.ExecutePostTranslate(tmp);
            }
            texts = await tmp.ToArrayAsync();
        }
        this.OcrTexts = texts;
        this.logger.LogDebug(sw.Elapsed.ToString());
    }

    private async Task TranslateAsync(IEnumerable<TextRect> texts)
    {
        try
        {
            var transTargets = texts.Select(w => w.Text).Distinct().Where(t => this.requesting.TryAdd(t, t) && !this.cache.Contains(t)).ToArray();
            if (!transTargets.Any())
            {
                return;
            }
            var translated = await this.translator.TranslateAsync(transTargets).ConfigureAwait(false);
            foreach (var t in transTargets)
            {
                this.requesting.TryRemove(t, out _);
            }
            this.cache.AddRange(transTargets.Zip(translated));
        }
        catch (Exception e)
        {
            this.timer.DisposeAsync().Forget();
            this.presentationService.ShowMessage(e.Message, icon: MessageBoxImage.Error);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            return;
        }
        if (disposing)
        {
            this.timer?.Dispose();
        }
        disposedValue = true;
    }

    public void Dispose()
    {
        // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

[OpenWindow]
public sealed class CaptureMainViewModel(
    [Inject] IPresentationService presentationService,
    [Inject] IProcessInfoStore processInfoStore,
    [Inject] ICaptureModule capture,
    [Inject] IOcrModule ocr,
    [Inject] ITranslateModule translator,
    [Inject] ICacheModule cache,
    [Inject] IColorModule color,
    [Inject] IEnumerable<IFilterModule> filters,
    [Inject] ILogger<CaptureMainViewModel> logger)
    : MainViewModelBase(presentationService, processInfoStore, capture, ocr, translator, cache, color, filters, logger)
{
    public ICaptureModule Capture { get; } = capture ?? throw new ArgumentNullException(nameof(capture));
}

[OpenWindow]
public sealed class OverlayMainViewModel(
    [Inject] IPresentationService presentationService,
    [Inject] IProcessInfoStore processInfoStore,
    [Inject] ICaptureModule capture,
    [Inject] IOcrModule ocr,
    [Inject] ITranslateModule translator,
    [Inject] ICacheModule cache,
    [Inject] IColorModule color,
    [Inject] IEnumerable<IFilterModule> filters,
    [Inject] ILogger<OverlayMainViewModel> logger)
    : MainViewModelBase(presentationService, processInfoStore, capture, ocr, translator, cache, color, filters, logger)
{
}
