using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Kamishibai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using R3;
using Windows.Graphics.Imaging;
using WindowTranslator.ComponentModel;
using WindowTranslator.Extensions;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Properties;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Main;

[ObservableObject]
public abstract partial class MainViewModelBase : IDisposable
{
    private readonly Subject<SoftwareBitmap> captureRequests = new();
    private readonly Subject<(IEnumerable<TextRect> Texts, FilterContext Context)> trackingRequests = new();
    private readonly Subject<TextRect[]> translationRequests = new();
    private readonly Subject<IEnumerable<TextRect>> uiRequests = new();
    private readonly IDisposable captureLoop;
    private readonly IDisposable trackingLoop;
    private readonly IDisposable translationLoop;
    private readonly IDisposable uiLoop;
    private readonly IOcrModule ocr;
    private readonly List<PriorityRect> priorityRects;
    private readonly IOcrTextTracker ocrTextTracker;
    private readonly OcrTraceRecorder ocrTraceRecorder;
    private readonly ITranslateModule translator;
    private readonly ICacheModule cache;
    private readonly IColorModule color;
    private readonly IEnumerable<IFilterModule> filters;
    private readonly ILogger logger;
    private readonly string name;
    private readonly IPresentationService presentationService;
    private readonly ICaptureModule capture;
    private readonly IProcessInfoStore processInfoStore;
    private readonly double fontScale;
    private readonly double overlayOpacity;
    private readonly double mousePointerHitTestPadding;
    private readonly bool isOneShotMode;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private double width = double.NaN;
    [ObservableProperty]
    private double height = double.NaN;

    [ObservableProperty]
    private bool overlayVisible = true;

    public bool DisplayBusy { get; }

    public BusyScope Recognizing { get; } = new();
    public BusyScope Filtering { get; } = new();

    private bool isFirstCapture = true;
    private volatile bool disposedValue;

    public ObservableCollection<TextRect> OcrTexts { get; } = [];
    public string Font { get; }
    public double MousePointerHitTestPadding => this.mousePointerHitTestPadding;

    public MainViewModelBase(
        IPresentationService presentationService,
        IOptionsSnapshot<TargetSettings> options,
        IProcessInfoStore processInfoStore,
        ICaptureModule capture,
        IOcrModule ocr,
        IOptionsSnapshot<BasicOcrParam> ocrParam,
        IOcrTextTracker ocrTextTracker,
        OcrTraceRecorder ocrTraceRecorder,
        ITranslateModule translator,
        ICacheModule cache,
        IColorModule color,
        IEnumerable<IFilterModule> filters,
        ILogger logger)
    {
        this.name = processInfoStore.Name;
        this.presentationService = presentationService;
        this.processInfoStore = processInfoStore;
        this.Font = options.Value.Font;
        this.fontScale = options.Value.FontScale;
        this.overlayOpacity = options.Value.OverlayOpacity;
        this.mousePointerHitTestPadding = options.Value.MousePointerHitTestPadding;
        this.isOneShotMode = options.Value.IsOneShotMode;
        this.DisplayBusy = options.Value.DisplayBusy;
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.capture.Captured += Capture_CapturedAsync;
        this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        this.priorityRects = ocrParam.Value.PriorityRects ?? [];
        this.ocrTextTracker = ocrTextTracker ?? throw new ArgumentNullException(nameof(ocrTextTracker));
        this.ocrTraceRecorder = ocrTraceRecorder ?? throw new ArgumentNullException(nameof(ocrTraceRecorder));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.color = color ?? throw new ArgumentNullException(nameof(color));
        this.filters = filters.ToArray();
        this.logger = logger;
        this.uiLoop = this.uiRequests
            .ObserveOn(new DispatcherSynchronizationContext(Application.Current.Dispatcher))
            .Subscribe(UpdateOcrTexts);
        var captureInterval = TimeSpan.FromSeconds(options.Value.CaptureInterval);
        var captures = !this.isOneShotMode && captureInterval > TimeSpan.Zero
            ? this.captureRequests.ThrottleLast(captureInterval)
            : this.captureRequests;
        this.captureLoop = captures
            .ObserveOnThreadPool()
            .SubscribeAwait(RecognizeAsync, AwaitOperation.ThrottleFirstLast, configureAwait: false);
        var previousSize = default(System.Drawing.Size);
        this.trackingLoop = Disposable.Combine(
            Observable.ObservePropertyChanged(this, x => x.OverlayVisible)
                .ObserveOnThreadPool()
                .Subscribe(_ => this.ocrTextTracker.Reset()),
            this.trackingRequests.SubscribeAwait(async (request, ct) =>
            {
                // 認識側の画像が破棄される前に、追跡の購読で使う画像を確保する。
                using var bitmap = SoftwareBitmap.Copy(request.Context.SoftwareBitmap);
                request.Context = request.Context with { SoftwareBitmap = bitmap };
                if (previousSize != request.Context.ImageSize)
                {
                    this.ocrTextTracker.Reset();
                    previousSize = request.Context.ImageSize;
                }
                // 切替時はタイマーを止め、処理の終了を待ってから画像を破棄する。
                await Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), CancellationToken.None)
                    .TakeUntil(ct)
                    .Where(_ => this.OverlayVisible)
                    .SelectAwait(async (_, _) =>
                    {
                        await ProcessTrackingAsync(request, ct);
                        return Unit.Default;
                    }, AwaitOperation.Drop, configureAwait: false)
                    .LastOrDefaultAsync(cancellationToken: CancellationToken.None);
            }, AwaitOperation.Switch, configureAwait: false));
        this.translationLoop = this.translationRequests
            .DistinctUntilChanged(EqualityComparer<TextRect[]>.Create((previous, current) =>
                previous!.Select(t => (t.SourceText, t.Context)).SequenceEqual(current!.Select(t => (t.SourceText, t.Context)))))
            .ObserveOnThreadPool()
            .SubscribeAwait(ProcessTranslationAsync, AwaitOperation.ThrottleFirstLast, configureAwait: false);
        if (!this.isOneShotMode)
        {
            this.capture.StartCapture(this.processInfoStore.MainWindowHandle);
        }
        var transAsm = this.translator.GetType().Assembly;
        this.title = $"{this.name} - {this.translator.Name} ({transAsm.GetName().Version})";
    }

    partial void OnOverlayVisibleChanged(bool value)
    {
        if (value)
        {
            this.uiRequests.OnNext([]);
            this.isFirstCapture = true;
            // Start capture when overlay becomes visible
            this.capture.StartCapture(this.processInfoStore.MainWindowHandle);
        }
        else
        {
            // Stop capture when overlay becomes hidden
            this.capture.StopCapture();
            this.logger.LogDebug("Overlay became hidden - capture stopped");
        }
    }

    private async Task Capture_CapturedAsync(object? sender, CapturedEventArgs args)
    {
        if (this.isOneShotMode)
        {
            if (!this.isFirstCapture)
            {
                return;
            }
            this.isFirstCapture = false;
            this.capture.StopCapture();
        }

        // フレームの寿命はイベント処理中だけなので、コピーの完了まで待つ。
        var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface);
        if (this.disposedValue)
        {
            bitmap.Dispose();
            return;
        }
        this.Width = bitmap.PixelWidth;
        this.Height = bitmap.PixelHeight;
        this.captureRequests.OnNext(bitmap);
    }

    private async ValueTask RecognizeAsync(SoftwareBitmap capturedBitmap, CancellationToken cancellationToken)
    {
        using var sbmp = capturedBitmap;
        IEnumerable<TextRect> texts;
        using (this.Recognizing.EnterBusy())
        {
            using var to = this.logger.LogDebugTime("画像認識");
            try
            {
                var regions = new List<OcrRegionInput>();
                if (this.priorityRects.Count == 0)
                {
                    regions.Add(new(new(0, 0, sbmp.PixelWidth, sbmp.PixelHeight)));
                }
                else
                {
                    foreach (var priorityRect in this.priorityRects)
                    {
                        var rect = priorityRect.ToAbsoluteRect(sbmp.PixelWidth, sbmp.PixelHeight);
                        var left = Math.Clamp(rect.Left, 0, sbmp.PixelWidth);
                        var top = Math.Clamp(rect.Top, 0, sbmp.PixelHeight);
                        var right = Math.Clamp(rect.Right, 0, sbmp.PixelWidth);
                        var bottom = Math.Clamp(rect.Bottom, 0, sbmp.PixelHeight);
                        if (right - left < 1 || bottom - top < 1)
                        {
                            continue;
                        }

                        var pixelLeft = Math.Floor(left);
                        var pixelTop = Math.Floor(top);
                        var pixelRight = Math.Ceiling(right);
                        var pixelBottom = Math.Ceiling(bottom);
                        regions.Add(new(
                            new(pixelLeft, pixelTop, pixelRight - pixelLeft, pixelBottom - pixelTop),
                            priorityRect.Keyword));
                    }
                }

                var observations = await this.ocr.RecognizeAsync(new(sbmp, regions));
                this.ocrTraceRecorder.Record(observations, new(sbmp.PixelWidth, sbmp.PixelHeight));
                texts = observations;
            }
            catch (ObjectDisposedException)
            {
                // すでに破棄されている場合は何もしない
                this.captureLoop.Dispose();
                await Application.Current.Dispatcher.InvokeAsync(this.capture.StopCapture);
                return;
            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合は何もしない
                this.captureLoop.Dispose();
                await Application.Current.Dispatcher.InvokeAsync(this.capture.StopCapture);
                return;
            }
            catch (Exception e)
            {
                this.captureLoop.Dispose();
                await Application.Current.Dispatcher.InvokeAsync(this.capture.StopCapture);
                var path = Path.Combine(PathUtility.UserDir, $"ocr_error", $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}.png");
                await sbmp.TrySaveImage(path);
                await ShowErrorAsync(Resources.FaildOcr, e, path);
                return;
            }
        }
        texts = (await this.color.ConvertColorAsync(sbmp, texts)).ToArray();

        cancellationToken.ThrowIfCancellationRequested();
        if (this.disposedValue)
        {
            return;
        }
        var context = new FilterContext
        {
            SoftwareBitmap = sbmp,
            ImageSize = new(sbmp.PixelWidth, sbmp.PixelHeight),
        };
        this.trackingRequests.OnNext((texts, context));
    }

    private async ValueTask ProcessTrackingAsync((IEnumerable<TextRect> Texts, FilterContext Context) request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<TextRect> texts = this.ocrTextTracker.Update(request.Texts, request.Context.ImageSize);
        texts = texts.Select(t => t with { FontSize = t.FontSize * this.fontScale });
        using (this.Filtering.EnterBusy())
        {
            var tmp = texts.ToAsyncEnumerable();
            foreach (var filter in this.filters.OrderByDescending(f => f.Priority))
            {
                tmp = filter.ExecutePreTranslate(tmp, request.Context);
            }
            using var t = this.logger.LogDebugTime("翻訳前処理");
            texts = await tmp.ToArrayAsync(cancellationToken);
        }
        texts = texts.Select(t => t switch
        {
            { TranslatedText: null } when this.cache.Contains(t.SourceText) => t with { TranslatedText = this.cache.Get(t.SourceText) },
            _ => t,
        }).ToArray();
        var requests = texts.Where(t => t.TranslatedText is null).ToArray();
        using (this.Filtering.EnterBusy())
        {
            var tmp = texts.Where(t => t.TranslatedText is not null).ToAsyncEnumerable();
            foreach (var filter in this.filters.OrderBy(f => f.Priority))
            {
                tmp = filter.ExecutePostTranslate(tmp, request.Context);
            }
            using var t = this.logger.LogDebugTime("翻訳後処理");
            texts = requests.Concat(await tmp.ToArrayAsync(cancellationToken)).ToArray();
        }
        cancellationToken.ThrowIfCancellationRequested();
        this.uiRequests.OnNext(texts);
        this.translationRequests.OnNext(requests);
    }

    private async ValueTask ProcessTranslationAsync(TextRect[] requests, CancellationToken cancellationToken)
    {
        if (requests.Length == 0)
        {
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (this.disposedValue)
            {
                this.logger.LogDebug("すでに破棄されているので翻訳キューを無視");
                return;
            }
            using var t = this.logger.LogDebugTime("翻訳");
            var translated = await this.translator.TranslateAsync(requests).ConfigureAwait(false);
            this.cache.AddRange(requests.Select(t => t.SourceText).Zip(translated));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "翻訳中にエラーが発生");
            this.captureLoop.Dispose();
            this.trackingLoop.Dispose();
            await Application.Current.Dispatcher.InvokeAsync(this.capture.StopCapture);
            this.translationLoop.Dispose();
            await ShowErrorAsync(Resources.FaildOverlay, e, string.Empty);
            return;
        }
    }

    private void UpdateOcrTexts(IEnumerable<TextRect> texts)
    {
        this.logger.LogDebug("UI適用");
        var hash = texts.Select(t => t with { Background = Color.FromArgb((int)(255 * this.overlayOpacity), t.Background) }).ToHashSet();
        foreach (var text in this.OcrTexts.Where(t => !hash.Contains(t)).ToArray())
        {
            this.OcrTexts.Remove(text);
        }
        hash.ExceptWith(this.OcrTexts);
        foreach (var text in hash)
        {
            this.OcrTexts.Add(text);
        }
    }

    private Task ShowErrorAsync(string message, Exception exception, string path)
        => Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await this.presentationService.OpenErrorDialogAsync(message, exception, this.name, path);
            StrongReferenceMessenger.Default.Send<CloseMessage>(new(this));
        }).Task.Unwrap();

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposedValue)
        {
            return;
        }
        this.disposedValue = true;
        if (disposing)
        {
            // 実行中の処理から遅れて通知されても受け付けない。
            this.captureRequests.OnCompleted();
            this.trackingLoop.Dispose();
            this.trackingRequests.OnCompleted();
            this.translationRequests.OnCompleted();
            this.uiRequests.OnCompleted();
            if (this.capture is IDisposable captureDisposable)
            {
                captureDisposable.Dispose();
            }
            this.captureLoop.Dispose();
            this.translationLoop.Dispose();
            this.uiLoop.Dispose();
        }
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
    [Inject] IOptionsSnapshot<TargetSettings> options,
    [Inject] IProcessInfoStore processInfoStore,
    [Inject] ICaptureModule capture,
    [Inject] IOcrModule ocr,
    [Inject] IOptionsSnapshot<BasicOcrParam> ocrParam,
    [Inject] IOcrTextTracker ocrTextTracker,
    [Inject] OcrTraceRecorder ocrTraceRecorder,
    [Inject] ITranslateModule translator,
    [Inject] ICacheModule cache,
    [Inject] IColorModule color,
    [Inject] IEnumerable<IFilterModule> filters,
    [Inject] ILogger<CaptureMainViewModel> logger)
    : MainViewModelBase(presentationService, options, processInfoStore, capture, ocr, ocrParam, ocrTextTracker, ocrTraceRecorder, translator, cache, color, filters, logger)
{
    public ICaptureModule Capture { get; } = capture ?? throw new ArgumentNullException(nameof(capture));
}

[OpenWindow]
public sealed class OverlayMainViewModel(
    [Inject] IPresentationService presentationService,
    [Inject] IOptionsSnapshot<TargetSettings> options,
    [Inject] IProcessInfoStore processInfoStore,
    [Inject] ICaptureModule capture,
    [Inject] IOcrModule ocr,
    [Inject] IOptionsSnapshot<BasicOcrParam> ocrParam,
    [Inject] IOcrTextTracker ocrTextTracker,
    [Inject] OcrTraceRecorder ocrTraceRecorder,
    [Inject] ITranslateModule translator,
    [Inject] ICacheModule cache,
    [Inject] IColorModule color,
    [Inject] IEnumerable<IFilterModule> filters,
    [Inject] ILogger<OverlayMainViewModel> logger)
    : MainViewModelBase(presentationService, options, processInfoStore, capture, ocr, ocrParam, ocrTextTracker, ocrTraceRecorder, translator, cache, color, filters, logger)
{
}
