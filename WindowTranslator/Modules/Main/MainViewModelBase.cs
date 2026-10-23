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
    private readonly Subject<(IEnumerable<TextRect> Texts, FilterContext Context)> translationRequests = new();
    private readonly Subject<IEnumerable<TextRect>> uiRequests = new();
    private readonly IDisposable captureLoop;
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
        var previousSize = default(System.Drawing.Size);
        this.captureLoop = captures
            .ObserveOnThreadPool()
            .SubscribeAwait(async (bitmap, ct) =>
            {
                var size = new System.Drawing.Size(bitmap.PixelWidth, bitmap.PixelHeight);
                if (!this.isOneShotMode && size != previousSize)
                {
                    this.ocrTextTracker.Reset();
                }
                previousSize = size;
                await RecognizeAsync(bitmap, ct);
            }, AwaitOperation.ThrottleFirstLast, configureAwait: false);
        this.translationLoop = this.translationRequests
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
            if (!this.isOneShotMode)
            {
                this.ocrTextTracker.Reset();
            }
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
        SoftwareBitmap? sbmp = capturedBitmap;
        using var bitmap = new DisposeAction(() => sbmp?.Dispose());
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
                texts = this.isOneShotMode
                    ? observations
                    : this.ocrTextTracker.Update(observations, new(sbmp.PixelWidth, sbmp.PixelHeight));
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
        texts = texts.Select(t => t with { FontSize = t.FontSize * this.fontScale });
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
        texts = texts.Select(t => t switch
        {
            { IsBusy: true } => t with { TranslatedText = null },
            { TranslatedText: null } when this.cache.Contains(t.SourceText) => t with { TranslatedText = this.cache.Get(t.SourceText) },
            _ => t,
        }).ToArray();
        if (this.isOneShotMode || this.isFirstCapture)
        {
            // 翻訳待ちの表示遅延を抑えるため、初回だけ認識結果を先に表示する。
            if (!this.isOneShotMode)
            {
                this.isFirstCapture = false;
            }
            this.uiRequests.OnNext(texts);
        }
        this.translationRequests.OnNext((texts, context));
        sbmp = null;
    }

    private async ValueTask ProcessTranslationAsync((IEnumerable<TextRect> Texts, FilterContext Context) request, CancellationToken cancellationToken)
    {
        using var bitmap = request.Context.SoftwareBitmap;
        cancellationToken.ThrowIfCancellationRequested();
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
                .Where(t => !t.IsBusy)
                .Where(t => t.TranslatedText is null)
                .ToArray();
            if (this.disposedValue)
            {
                this.logger.LogDebug("すでに破棄されているので翻訳キューを無視");
                return;
            }
            if (requests.Length > 0)
            {
                using var t = this.logger.LogDebugTime("翻訳");
                var translated = await this.translator.TranslateAsync(requests).ConfigureAwait(false);
                this.cache.AddRange(requests.Select(t => t.SourceText).Zip(translated));
                var translatedIndex = 0;
                texts = texts.Select(t => t.TranslatedText is null && !t.IsBusy
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
        this.uiRequests.OnNext(texts);
    }

    private void UpdateOcrTexts(IEnumerable<TextRect> texts)
    {
        this.logger.LogDebug("UI適用");
        // 翻訳待ちは表示用の複製にだけ付ける。
        var hash = texts.Select(t => t with
        {
            Background = Color.FromArgb((int)(255 * this.overlayOpacity), t.Background),
            BusyReasons = t.TranslatedText is null && !t.IsBusy
                ? TextRegionBusyReason.Translation
                : t.BusyReasons,
        }).ToHashSet();
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
