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
    private readonly Subject<CaptureRequest> captureRequests = new();
    private readonly Subject<TrackingSignal> trackingRequests = new();
    private readonly Subject<(IEnumerable<TextRect> Texts, FilterContext Context, int VisibilityGeneration)> translationRequests = new();
    private readonly Subject<(IEnumerable<TextRect> Texts, int VisibilityGeneration)> uiRequests = new();
    private readonly object ocrTextTrackerLock = new();
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
    private int visibilityGeneration;

    public ObservableCollection<TextRect> OcrTexts { get; } = [];
    public string Font { get; }
    public double MousePointerHitTestPadding => this.mousePointerHitTestPadding;

    private bool IsCurrentVisibleGeneration(int generation)
        => !this.disposedValue
            && this.OverlayVisible
            && generation == Volatile.Read(ref this.visibilityGeneration);

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
            .Where(request => this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
            .Subscribe(request => UpdateOcrTexts(request.Texts));
        var captureInterval = TimeSpan.FromSeconds(options.Value.CaptureInterval);
        var captures = !this.isOneShotMode && captureInterval > TimeSpan.Zero
            ? this.captureRequests.ThrottleLast(captureInterval)
            : this.captureRequests;
        this.captureLoop = captures
            .ObserveOnThreadPool()
            .SubscribeAwait(RecognizeAsync, AwaitOperation.ThrottleFirstLast, configureAwait: false);
        this.trackingLoop = Disposable.Combine(
            Disposable.Create(() => this.trackingRequests.OnCompleted()),
            Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), TimeProvider.System)
                .Where(_ => this.OverlayVisible)
                .Subscribe(_ => this.trackingRequests.OnNext(new(TrackingSignalKind.Tick))),
            this.trackingRequests
                .Materialize()
                .Synchronize()
                .Scan(new TrackingUpdate(null, null), (previous, current) =>
                {
                    if (current.Kind != NotificationKind.OnNext)
                    {
                        previous.Current?.Context.SoftwareBitmap.Dispose();
                        return new(null, null);
                    }

                    switch (current.Value.Kind)
                    {
                        case TrackingSignalKind.Reset:
                            previous.Current?.Context.SoftwareBitmap.Dispose();
                            this.ResetOcrTextTracker();
                            return new(null, null);
                        case TrackingSignalKind.Observation:
                            var request = current.Value.Request!;
                            if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
                            {
                                request.Context.SoftwareBitmap.Dispose();
                                return new(previous.Current, null);
                            }
                            if (previous.Current?.Context.ImageSize != request.Context.ImageSize)
                            {
                                this.ResetOcrTextTracker();
                            }
                            previous.Current?.Context.SoftwareBitmap.Dispose();
                            this.isFirstCapture |= this.isOneShotMode;
                            return new(request, request);
                        case TrackingSignalKind.Tick:
                            if (previous.Current is not { } previousRequest)
                            {
                                return new(null, null);
                            }
                            if (this.IsCurrentVisibleGeneration(previousRequest.VisibilityGeneration))
                            {
                                return new(previousRequest, previousRequest);
                            }
                            previousRequest.Context.SoftwareBitmap.Dispose();
                            this.ResetOcrTextTracker();
                            return new(null, null);
                        default:
                            throw new ArgumentOutOfRangeException(nameof(current), current.Value.Kind, null);
                    }
                })
                .Where(update => update.Work is not null)
                .Select(update => update.Work!)
                .SubscribeAwait(async (request, ct) =>
                {
                    if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
                    {
                        return;
                    }
                    using var bitmap = request.Context.SoftwareBitmap.GetReadOnlyView();
                    var texts = await ProcessTrackingAsync(request, bitmap, ct);
                    if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
                    {
                        return;
                    }
                    if (this.isFirstCapture)
                    {
                        this.isFirstCapture = false;
                        this.uiRequests.OnNext((texts, request.VisibilityGeneration));
                    }
                    if (this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
                    {
                        this.translationRequests.OnNext((texts, request.Context with { SoftwareBitmap = bitmap.GetReadOnlyView(), }, request.VisibilityGeneration));
                    }
                }, AwaitOperation.Drop, configureAwait: false, cancelOnCompleted: true));
        this.translationLoop = this.translationRequests
            .SubscribeAwaitThrottleFirstLast(ProcessTranslationAsync, static r => r.Context.SoftwareBitmap.Dispose(), configureAwait: false);
        if (!this.isOneShotMode)
        {
            this.capture.StartCapture(this.processInfoStore.MainWindowHandle);
        }
        var transAsm = this.translator.GetType().Assembly;
        this.title = $"{this.name} - {this.translator.Name} ({transAsm.GetName().Version})";
    }

    partial void OnOverlayVisibleChanged(bool value)
    {
        var generation = Interlocked.Increment(ref this.visibilityGeneration);
        this.trackingRequests.OnNext(new(TrackingSignalKind.Reset));
        if (value)
        {
            this.uiRequests.OnNext(([], generation));
            this.isFirstCapture = !this.isOneShotMode;
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
        var generation = Volatile.Read(ref this.visibilityGeneration);
        if (!this.IsCurrentVisibleGeneration(generation))
        {
            return;
        }
        if (this.isOneShotMode)
        {
            this.capture.StopCapture();
        }

        // フレームの寿命はイベント処理中だけなので、コピーの完了まで待つ。
        var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface);
        if (this.disposedValue || !this.IsCurrentVisibleGeneration(generation))
        {
            bitmap.Dispose();
            return;
        }
        this.Width = bitmap.PixelWidth;
        this.Height = bitmap.PixelHeight;
        this.captureRequests.OnNext(new(bitmap, generation));
    }

    private async ValueTask RecognizeAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        var capturedBitmap = request.Bitmap;
        using var bitmap = new DisposeAction(capturedBitmap.Dispose);
        if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            return;
        }
        IEnumerable<TextRect> texts;
        using (this.Recognizing.EnterBusy())
        {
            using var to = this.logger.LogDebugTime("画像認識");
            try
            {
                var regions = new List<OcrRegionInput>();
                if (this.priorityRects.Count == 0)
                {
                    regions.Add(new(new(0, 0, capturedBitmap.PixelWidth, capturedBitmap.PixelHeight)));
                }
                else
                {
                    foreach (var priorityRect in this.priorityRects)
                    {
                        var rect = priorityRect.ToAbsoluteRect(capturedBitmap.PixelWidth, capturedBitmap.PixelHeight);
                        var left = Math.Clamp(rect.Left, 0, capturedBitmap.PixelWidth);
                        var top = Math.Clamp(rect.Top, 0, capturedBitmap.PixelHeight);
                        var right = Math.Clamp(rect.Right, 0, capturedBitmap.PixelWidth);
                        var bottom = Math.Clamp(rect.Bottom, 0, capturedBitmap.PixelHeight);
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

                var observations = await this.ocr.RecognizeAsync(new(capturedBitmap, regions));
                this.ocrTraceRecorder.Record(observations, new(capturedBitmap.PixelWidth, capturedBitmap.PixelHeight));
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
                await capturedBitmap.TrySaveImage(path);
                await ShowErrorAsync(Resources.FaildOcr, e, path);
                return;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (this.disposedValue || !this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            return;
        }
        var context = new FilterContext
        {
            SoftwareBitmap = capturedBitmap.GetReadOnlyView(),
            ImageSize = new(capturedBitmap.PixelWidth, capturedBitmap.PixelHeight),
        };
        this.trackingRequests.OnNext(new(TrackingSignalKind.Observation, new(texts, context, request.VisibilityGeneration)));
    }

    internal static IEnumerable<TextRect> SelectOcrTexts(
        IEnumerable<TextRect> observations, System.Drawing.Size imageSize, IOcrTextTracker tracker, bool isOneShotMode)
        => isOneShotMode ? observations : tracker.Update(observations, imageSize);

    private void ResetOcrTextTracker()
    {
        lock (this.ocrTextTrackerLock)
        {
            this.ocrTextTracker.Reset();
        }
    }

    private async ValueTask<IEnumerable<TextRect>> ProcessTrackingAsync(TrackingRequest request, SoftwareBitmap bitmap, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<TextRect> trackedTexts;
        lock (this.ocrTextTrackerLock)
        {
            if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
            {
                return [];
            }
            trackedTexts = SelectOcrTexts(request.Texts, request.Context.ImageSize, this.ocrTextTracker, this.isOneShotMode);
        }
        if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            return [];
        }
        var texts = trackedTexts
            .Select(t => t with { FontSize = t.FontSize * this.fontScale });
        if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            return [];
        }
        texts = (await this.color.ConvertColorAsync(bitmap, texts)).ToArray();
        if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            return [];
        }
        texts = texts.Select(t => t switch
        {
            { TranslatedText: null } when this.cache.Contains(t.SourceText) => t with { TranslatedText = this.cache.Get(t.SourceText) },
            _ => t,
        }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return texts;
    }

    private async ValueTask ProcessTranslationAsync((IEnumerable<TextRect> Texts, FilterContext Context, int VisibilityGeneration) request, CancellationToken cancellationToken)
    {
        using var bitmap = request.Context.SoftwareBitmap;
        cancellationToken.ThrowIfCancellationRequested();
        if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            return;
        }
        IEnumerable<TextRect> texts;
        using (this.Filtering.EnterBusy())
        {
            var tmp = request.Texts.ToAsyncEnumerable();
            foreach (var filter in this.filters.OrderByDescending(f => f.Priority))
            {
                tmp = filter.ExecutePreTranslate(tmp, request.Context);
            }
            using var t = this.logger.LogDebugTime("翻訳前処理");
            texts = await tmp.ToArrayAsync(cancellationToken);
        }
        if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            return;
        }
        try
        {
            var requests = texts
                .Where(t => t.TranslatedText is null)
                .ToArray();
            if (requests.Length > 0)
            {
                if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
                {
                    return;
                }
                using var t = this.logger.LogDebugTime("翻訳");
                var translated = await this.translator.TranslateAsync(requests).ConfigureAwait(false);
                if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
                {
                    return;
                }
                this.cache.AddRange(requests.Select(t => t.SourceText).Zip(translated));
                var translatedIndex = 0;
                texts = texts.Select(t => t.TranslatedText is null
                    ? t with { TranslatedText = translated[translatedIndex++] }
                    : t).ToArray();
            }
            else
            {
                this.logger.LogDebug("翻訳キューに未翻訳がないので翻訳を省略");
            }
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
        if (!this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            return;
        }
        using (this.Filtering.EnterBusy())
        {
            var tmp = texts.ToAsyncEnumerable();
            foreach (var filter in this.filters.OrderBy(f => f.Priority))
            {
                tmp = filter.ExecutePostTranslate(tmp, request.Context);
            }
            using var t = this.logger.LogDebugTime("翻訳後処理");
            texts = await tmp.ToArrayAsync(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (this.IsCurrentVisibleGeneration(request.VisibilityGeneration))
        {
            this.uiRequests.OnNext((texts, request.VisibilityGeneration));
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

    private sealed record CaptureRequest(SoftwareBitmap Bitmap, int VisibilityGeneration);

    private sealed record TrackingRequest(
        IEnumerable<TextRect> Texts,
        FilterContext Context,
        int VisibilityGeneration);

    private sealed record TrackingSignal(TrackingSignalKind Kind, TrackingRequest? Request = null);

    private sealed record TrackingUpdate(TrackingRequest? Current, TrackingRequest? Work);

    private enum TrackingSignalKind
    {
        Tick,
        Observation,
        Reset,
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
            this.translationRequests.OnCompleted();
            this.uiRequests.OnCompleted();
            if (this.capture is IDisposable captureDisposable)
            {
                captureDisposable.Dispose();
            }
            this.captureLoop.Dispose();
            this.trackingLoop.Dispose();
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
