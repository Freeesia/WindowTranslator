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
    private readonly Subject<(SoftwareBitmap Bitmap, long Generation)> captureRequests = new();
    private readonly Subject<(IEnumerable<TextRect> Texts, FilterContext Context)> trackingRequests = new();
    private readonly Subject<(IEnumerable<TextRect> Texts, FilterContext Context, long Generation)> translationRequests = new();
    private readonly Subject<(IEnumerable<TextRect> Texts, long? Generation)> uiRequests = new();
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
    private long captureGeneration;
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
        : this(presentationService, options, processInfoStore, capture, ocr, ocrParam, ocrTextTracker,
            ocrTraceRecorder, translator, cache, color, filters, logger,
            new DispatcherSynchronizationContext(Application.Current.Dispatcher))
    {
    }

    internal MainViewModelBase(
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
        ILogger logger,
        SynchronizationContext uiSynchronizationContext)
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
            .ObserveOn(uiSynchronizationContext)
            .Where(request => this.OverlayVisible
                && (request.Generation is null || IsCurrentCapture(request.Generation.Value)))
            .Subscribe(request => UpdateOcrTexts(request.Texts));
        var captureInterval = TimeSpan.FromSeconds(options.Value.CaptureInterval);
        var captures = !this.isOneShotMode && captureInterval > TimeSpan.Zero
            ? this.captureRequests.ThrottleLast(captureInterval)
            : this.captureRequests;
        this.captureLoop = captures
            .ObserveOnThreadPool()
            .SubscribeAwaitThrottleFirstLast(RecognizeAsync, static request => request.Bitmap.Dispose(), configureAwait: false);
        this.trackingLoop = this.isOneShotMode
            ? Disposable.Create(() => this.trackingRequests.OnCompleted())
            : Disposable.Combine(
                Disposable.Create(() => this.trackingRequests.OnCompleted()),
                this.ObservePropertyChanged(x => x.OverlayVisible).Where(v => v).ObserveOnThreadPool().Subscribe(_ => this.ocrTextTracker.Reset()),
                Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), TimeProvider.System).Subscribe(_ => this.trackingRequests.OnNext(default)),
                this.trackingRequests
                    .Materialize()
                    .Synchronize()
                    // タイマーの空通知では直前のOCR結果を再利用する。
                    .Scan(default((IEnumerable<TextRect> Texts, FilterContext Context)), (previous, current) =>
                    {
                        if (current.Value.Context is { } context)
                        {
                            if (previous.Context?.ImageSize != context.ImageSize)
                            {
                                this.ocrTextTracker.Reset();
                            }
                        }
                        else if (current.Kind == NotificationKind.OnNext)
                        {
                            return previous;
                        }
                        previous.Context?.SoftwareBitmap.Dispose();
                        return current.Value;
                    })
                    .Where(request => request.Context is not null)
                    .SubscribeAwait(async (request, ct) =>
                    {
                        using var bitmap = request.Context.SoftwareBitmap.GetReadOnlyView();
                        var texts = await PrepareOcrTextsAsync(request, bitmap, ct);
                        if (this.isFirstCapture)
                        {
                            this.isFirstCapture = false;
                            this.uiRequests.OnNext((texts, null));
                        }
                        this.translationRequests.OnNext((texts, request.Context with { SoftwareBitmap = bitmap.GetReadOnlyView(), }, 0));
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
        if (this.isOneShotMode)
        {
            // 表示切り替え後に以前のワンショットの結果を反映しない。
            Interlocked.Increment(ref this.captureGeneration);
        }
        if (value)
        {
            this.uiRequests.OnNext(([], null));
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
        var generation = NextCaptureGeneration();
        if (this.isOneShotMode)
        {
            this.capture.StopCapture();
        }

        // フレームの寿命はイベント処理中だけなので、コピーの完了まで待つ。
        var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface);
        EnqueueCapture(bitmap, generation);
    }

    internal void EnqueueCapture(SoftwareBitmap bitmap)
        => EnqueueCapture(bitmap, NextCaptureGeneration());

    private void EnqueueCapture(SoftwareBitmap bitmap, long generation)
    {
        if (this.disposedValue || !IsCurrentCapture(generation))
        {
            bitmap.Dispose();
            return;
        }
        this.Width = bitmap.PixelWidth;
        this.Height = bitmap.PixelHeight;
        this.captureRequests.OnNext((bitmap, generation));
    }

    private long NextCaptureGeneration()
        => this.isOneShotMode ? Interlocked.Increment(ref this.captureGeneration) : 0;

    private bool IsCurrentCapture(long generation)
        => !this.isOneShotMode || generation == Interlocked.Read(ref this.captureGeneration);

    private async ValueTask RecognizeAsync((SoftwareBitmap Bitmap, long Generation) request, CancellationToken cancellationToken)
    {
        var capturedBitmap = request.Bitmap;
        using var bitmap = new DisposeAction(capturedBitmap.Dispose);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.disposedValue || !IsCurrentCapture(request.Generation))
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
        if (this.disposedValue || !IsCurrentCapture(request.Generation))
        {
            return;
        }
        var context = new FilterContext
        {
            SoftwareBitmap = capturedBitmap.GetReadOnlyView(),
            ImageSize = new(capturedBitmap.PixelWidth, capturedBitmap.PixelHeight),
        };
        if (!this.isOneShotMode)
        {
            this.trackingRequests.OnNext((texts, context));
            return;
        }

        // ワンショットでは過去の観測を追跡せず、今回のOCR結果をそのまま処理する。
        using var bitmapView = context.SoftwareBitmap;
        var currentTexts = await PrepareOcrTextsAsync((texts, context), bitmapView, cancellationToken);
        if (this.disposedValue || !IsCurrentCapture(request.Generation))
        {
            return;
        }
        this.uiRequests.OnNext((currentTexts, request.Generation));
        this.translationRequests.OnNext((currentTexts, context with { SoftwareBitmap = bitmapView.GetReadOnlyView() }, request.Generation));
    }

    private async ValueTask<IEnumerable<TextRect>> PrepareOcrTextsAsync((IEnumerable<TextRect> Texts, FilterContext Context) request, SoftwareBitmap bitmap, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observations = this.isOneShotMode
            ? request.Texts
            : this.ocrTextTracker.Update(request.Texts, request.Context.ImageSize);
        var texts = observations.Select(t => t with { FontSize = t.FontSize * this.fontScale });
        texts = (await this.color.ConvertColorAsync(bitmap, texts)).ToArray();
        texts = texts.Select(t => t switch
        {
            { TranslatedText: null } when this.cache.Contains(t.SourceText) => t with { TranslatedText = this.cache.Get(t.SourceText) },
            _ => t,
        }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return texts;
    }

    private async ValueTask ProcessTranslationAsync((IEnumerable<TextRect> Texts, FilterContext Context, long Generation) request, CancellationToken cancellationToken)
    {
        using var bitmap = request.Context.SoftwareBitmap;
        cancellationToken.ThrowIfCancellationRequested();
        if (this.disposedValue || !this.OverlayVisible || !IsCurrentCapture(request.Generation))
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
        if (!IsCurrentCapture(request.Generation))
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
                using var t = this.logger.LogDebugTime("翻訳");
                var translated = await this.translator.TranslateAsync(requests).ConfigureAwait(false);
                if (!IsCurrentCapture(request.Generation))
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
        this.uiRequests.OnNext((texts, this.isOneShotMode ? request.Generation : null));
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
