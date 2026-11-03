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
using Microsoft.VisualStudio.Threading;
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
    private readonly Dispatcher dispatcher = Application.Current.Dispatcher;
    private readonly Subject<SoftwareBitmap> captureRequests = new();
    private readonly Subject<SoftwareBitmap?> recognitionRequests = new();
    private readonly Subject<(IEnumerable<TextRect> Texts, FilterContext Context)> trackingRequests = new();
    private readonly Subject<(IEnumerable<TextRect> Texts, FilterContext Context)?> translationRequests = new();
    private IDisposable captureEvents;
    private readonly IDisposable captureInput;
    private readonly IDisposable captureLoop;
    private readonly IDisposable trackingLoop;
    private readonly IDisposable translationLoop;
    private readonly DispatcherTimer trackingTimer;
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
    private readonly TimeSpan captureInterval;

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
    private bool needsTrackerReset;
    private bool updatingOcrTexts;
    private SoftwareBitmap? pendingCapture;
    private volatile bool disposedValue;
    private event Action? OverlayHidden;

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
        this.captureInterval = TimeSpan.FromSeconds(options.Value.CaptureInterval);
        this.DisplayBusy = options.Value.DisplayBusy;
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.captureEvents = CreateCaptureEvents();
        this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        this.priorityRects = ocrParam.Value.PriorityRects ?? [];
        this.ocrTextTracker = ocrTextTracker ?? throw new ArgumentNullException(nameof(ocrTextTracker));
        this.ocrTraceRecorder = ocrTraceRecorder ?? throw new ArgumentNullException(nameof(ocrTraceRecorder));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.color = color ?? throw new ArgumentNullException(nameof(color));
        this.filters = filters.ToArray();
        this.logger = logger;
        // 処理開始と非表示通知を UI スレッドで順序付ける。
        this.captureLoop = this.dispatcher.Invoke(() => this.recognitionRequests
            .SubscribeAwaitThrottleFirstLast(
                (bitmap, ct) => bitmap is null ? ValueTask.CompletedTask : RecognizeAsync(bitmap, ct),
                static bitmap => bitmap?.Dispose(), configureAwait: true));
        this.captureInput = !this.isOneShotMode && this.captureInterval > TimeSpan.Zero
            ? this.captureRequests.ThrottleLast(this.captureInterval).Subscribe(bitmap =>
            {
                _ = this.dispatcher.InvokeAsync(() =>
                {
                    if (ReferenceEquals(Interlocked.CompareExchange(ref this.pendingCapture, null, bitmap), bitmap))
                    {
                        this.recognitionRequests.OnNext(bitmap);
                    }
                });
            })
            : Disposable.Empty;
        this.trackingTimer = new DispatcherTimer(DispatcherPriority.Normal, this.dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        this.trackingTimer.Tick += (_, _) => this.trackingRequests.OnNext(default);
        this.trackingLoop = Disposable.Combine(
            Disposable.Create(() => this.trackingRequests.OnCompleted()),
            this.trackingRequests
                .Materialize()
                .Synchronize()
                // タイマーの空通知では直前のOCR結果を再利用する。
                .Scan(default((IEnumerable<TextRect> Texts, FilterContext Context)), (previous, current) =>
                {
                    if (!this.OverlayVisible)
                    {
                        previous.Context?.SoftwareBitmap.Dispose();
                        return default;
                    }
                    if (current.Value.Context is { } context)
                    {
                        if (previous.Context?.ImageSize != context.ImageSize)
                        {
                            this.needsTrackerReset = true;
                        }
                        this.isFirstCapture |= this.isOneShotMode;
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
                    var hidden = new TaskCompletionSource();
                    using var visibility = ObserveHiding(hidden);
                    using var bitmap = request.Context.SoftwareBitmap.GetReadOnlyView();
                    var reset = this.needsTrackerReset;
                    this.needsTrackerReset = false;
                    // 重い追跡処理は UI スレッドを離れて実行する。
                    await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                    ct.ThrowIfCancellationRequested();
                    if (hidden.Task.IsCompleted) return;
                    if (reset) this.ocrTextTracker.Reset();
                    if (hidden.Task.IsCompleted) return;
                    var texts = await ProcessTrackingAsync(request, bitmap, hidden.Task, ct);
                    if (texts is null)
                    {
                        return;
                    }
                    await this.dispatcher.InvokeAsync(() =>
                    {
                        if (hidden.Task.IsCompleted || this.disposedValue) return;
                        if (this.isFirstCapture)
                        {
                            this.isFirstCapture = false;
                            UpdateOcrTexts(texts, hidden.Task);
                        }
                        if (hidden.Task.IsCompleted) return;
                        this.translationRequests.OnNext((texts, request.Context with { SoftwareBitmap = bitmap.GetReadOnlyView(), }));
                    });
                }, AwaitOperation.Drop, configureAwait: false, cancelOnCompleted: true));
        this.translationLoop = this.dispatcher.Invoke(() => this.translationRequests
            .SubscribeAwaitThrottleFirstLast(
                (request, ct) => request is { } value ? ProcessTranslationAsync(value, ct) : ValueTask.CompletedTask,
                static request => request?.Context.SoftwareBitmap.Dispose(), configureAwait: true));
        this.trackingTimer.Start();
        if (!this.isOneShotMode)
        {
            this.capture.StartCapture(this.processInfoStore.MainWindowHandle);
        }
        var transAsm = this.translator.GetType().Assembly;
        this.title = $"{this.name} - {this.translator.Name} ({transAsm.GetName().Version})";
    }

    private IDisposable ObserveHiding(TaskCompletionSource hidden)
    {
        void Handler() => hidden.TrySetResult();
        this.OverlayHidden += Handler;
        return Disposable.Create(() => this.OverlayHidden -= Handler);
    }

    private void QueueCapture(SoftwareBitmap bitmap)
    {
        if (this.isOneShotMode || this.captureInterval == TimeSpan.Zero)
        {
            this.recognitionRequests.OnNext(bitmap);
            return;
        }
        Interlocked.Exchange(ref this.pendingCapture, bitmap)?.Dispose();
        this.captureRequests.OnNext(bitmap);
    }

    private void ClearPendingCapture()
        => Interlocked.Exchange(ref this.pendingCapture, null)?.Dispose();

    partial void OnOverlayVisibleChanged(bool value)
    {
        if (value)
        {
            this.isFirstCapture = !this.isOneShotMode;
            this.captureEvents = CreateCaptureEvents();
            if (this.updatingOcrTexts)
            {
                _ = this.dispatcher.InvokeAsync(this.OcrTexts.Clear, DispatcherPriority.Send);
            }
            else if (this.OcrTexts.Count > 0)
            {
                this.OcrTexts.Clear();
            }
            if (this.OverlayVisible && !this.trackingTimer.IsEnabled)
            {
                this.trackingTimer.Start();
                // Start capture when overlay becomes visible
                this.capture.StartCapture(this.processInfoStore.MainWindowHandle);
            }
        }
        else
        {
            this.OverlayHidden?.Invoke();
            this.captureEvents.Dispose();
            ClearPendingCapture();
            this.recognitionRequests.OnNext(null);
            this.translationRequests.OnNext(null);
            this.trackingTimer.Stop();
            this.trackingRequests.OnNext(default);
            // Stop capture when overlay becomes hidden
            this.capture.StopCapture();
            this.logger.LogDebug("Overlay became hidden - capture stopped");
        }
    }

    private IDisposable CreateCaptureEvents()
        => Observable.Create<SoftwareBitmap>(observer =>
        {
            AsyncEventHandler<CapturedEventArgs> handler = (_, args) => Capture_CapturedAsync(args, observer);
            this.capture.Captured += handler;
            return Disposable.Create(() => this.capture.Captured -= handler);
        }, rawObserver: true).Subscribe(QueueCapture);

    private async Task Capture_CapturedAsync(CapturedEventArgs args, Observer<SoftwareBitmap> observer)
    {
        var accepted = await this.dispatcher.InvokeAsync(() =>
        {
            if (this.disposedValue || observer.IsDisposed) return false;
            if (this.isOneShotMode) this.capture.StopCapture();
            return true;
        });
        if (!accepted) return;

        // フレームの寿命はイベント処理中だけなので、コピーの完了まで待つ。
        var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface);
        var handedOff = false;
        try
        {
            await this.dispatcher.InvokeAsync(() =>
            {
                if (this.disposedValue || observer.IsDisposed) return;
                this.Width = bitmap.PixelWidth;
                this.Height = bitmap.PixelHeight;
                if (this.disposedValue || observer.IsDisposed) return;
                observer.OnNext(bitmap);
                handedOff = true;
            });
        }
        finally
        {
            if (!handedOff) bitmap.Dispose();
        }
    }

    private async ValueTask RecognizeAsync(SoftwareBitmap capturedBitmap, CancellationToken cancellationToken)
    {
        using var bitmap = new DisposeAction(capturedBitmap.Dispose);
        var hidden = new TaskCompletionSource();
        using var visibility = ObserveHiding(hidden);
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.disposedValue || hidden.Task.IsCompleted)
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
                await StopRecognitionAsync(hidden.Task);
                return;
            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合は何もしない
                await StopRecognitionAsync(hidden.Task);
                return;
            }
            catch (Exception e)
            {
                if (!await StopRecognitionAsync(hidden.Task))
                {
                    this.logger.LogError(e, "非表示前の画像認識でエラーが発生");
                    return;
                }
                var path = Path.Combine(PathUtility.UserDir, $"ocr_error", $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}.png");
                await capturedBitmap.TrySaveImage(path);
                await ShowErrorAsync(Resources.FaildOcr, e, path);
                return;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        await this.dispatcher.InvokeAsync(() =>
        {
            if (this.disposedValue || hidden.Task.IsCompleted) return;
            var context = new FilterContext
            {
                SoftwareBitmap = capturedBitmap.GetReadOnlyView(),
                ImageSize = new(capturedBitmap.PixelWidth, capturedBitmap.PixelHeight),
            };
            this.trackingRequests.OnNext((texts, context));
        });
    }

    private async Task<bool> StopRecognitionAsync(Task hidden)
        => await this.dispatcher.InvokeAsync(() =>
        {
            if (this.disposedValue || hidden.IsCompleted) return false;
            this.captureEvents.Dispose();
            ClearPendingCapture();
            this.captureInput.Dispose();
            this.captureLoop.Dispose();
            this.capture.StopCapture();
            return true;
        });

    internal static IEnumerable<TextRect> SelectOcrTexts(
        IEnumerable<TextRect> observations, System.Drawing.Size imageSize, IOcrTextTracker tracker, bool isOneShotMode)
        => isOneShotMode ? observations : tracker.Update(observations, imageSize);

    private async ValueTask<IEnumerable<TextRect>?> ProcessTrackingAsync(
        (IEnumerable<TextRect> Texts, FilterContext Context) request,
        SoftwareBitmap bitmap,
        Task hidden,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var texts = SelectOcrTexts(request.Texts, request.Context.ImageSize, this.ocrTextTracker, this.isOneShotMode)
            .Select(t => t with { FontSize = t.FontSize * this.fontScale });
        if (hidden.IsCompleted)
        {
            return null;
        }
        texts = (await this.color.ConvertColorAsync(bitmap, texts)).ToArray();
        if (hidden.IsCompleted)
        {
            return null;
        }
        texts = texts.Select(t => t switch
        {
            { TranslatedText: null } when this.cache.Contains(t.SourceText) => t with { TranslatedText = this.cache.Get(t.SourceText) },
            _ => t,
        }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return texts;
    }

    private async ValueTask ProcessTranslationAsync((IEnumerable<TextRect> Texts, FilterContext Context) request, CancellationToken cancellationToken)
    {
        using var bitmap = request.Context.SoftwareBitmap;
        var hidden = new TaskCompletionSource();
        using var visibility = ObserveHiding(hidden);
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.disposedValue || hidden.Task.IsCompleted)
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
        try
        {
            var requests = texts
                .Where(t => t.TranslatedText is null)
                .ToArray();
            if (requests.Length > 0)
            {
                using var t = this.logger.LogDebugTime("翻訳");
                if (hidden.Task.IsCompleted) return;
                var translated = await this.translator.TranslateAsync(requests).ConfigureAwait(false);
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
            var stopped = await this.dispatcher.InvokeAsync(() =>
            {
                if (hidden.Task.IsCompleted || this.disposedValue) return false;
                this.captureEvents.Dispose();
                ClearPendingCapture();
                this.captureInput.Dispose();
                this.captureLoop.Dispose();
                this.trackingTimer.Stop();
                this.trackingLoop.Dispose();
                this.translationLoop.Dispose();
                this.capture.StopCapture();
                return true;
            });
            if (stopped) await ShowErrorAsync(Resources.FaildOverlay, e, string.Empty);
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
        await this.dispatcher.InvokeAsync(() =>
        {
            if (!this.disposedValue)
            {
                UpdateOcrTexts(texts, hidden.Task);
            }
        });
    }

    private void UpdateOcrTexts(IEnumerable<TextRect> texts, Task hidden)
    {
        this.logger.LogDebug("UI適用");
        var hash = texts.Select(t => t with { Background = Color.FromArgb((int)(255 * this.overlayOpacity), t.Background) }).ToHashSet();
        this.updatingOcrTexts = true;
        try
        {
            foreach (var text in this.OcrTexts.Where(t => !hash.Contains(t)).ToArray())
            {
                if (hidden.IsCompleted) return;
                this.OcrTexts.Remove(text);
            }
            hash.ExceptWith(this.OcrTexts);
            foreach (var text in hash)
            {
                if (hidden.IsCompleted) return;
                this.OcrTexts.Add(text);
            }
        }
        finally
        {
            this.updatingOcrTexts = false;
        }
    }

    private Task ShowErrorAsync(string message, Exception exception, string path)
        => this.dispatcher.InvokeAsync(async () =>
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
            this.captureEvents.Dispose();
            ClearPendingCapture();
            this.captureRequests.OnCompleted();
            this.recognitionRequests.OnCompleted();
            this.translationRequests.OnCompleted();
            if (this.capture is IDisposable captureDisposable)
            {
                captureDisposable.Dispose();
            }
            this.captureInput.Dispose();
            this.captureLoop.Dispose();
            this.trackingTimer.Stop();
            this.trackingLoop.Dispose();
            this.translationLoop.Dispose();
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
