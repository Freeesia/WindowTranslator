using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Threading.Channels;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WindowTranslator.Modules.Main;

[ObservableObject]
public abstract partial class MainViewModelBase : IDisposable
{
    // 待機中は最新の要求だけを保持し、置き換えた画像はチャネルが破棄する。
    private readonly Channel<SoftwareBitmap> captureRequests = Channel.CreateBounded<SoftwareBitmap>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest }, bitmap => bitmap.Dispose());
    private readonly Channel<(TextRect[] Texts, FilterContext Context)> translationRequests =
        Channel.CreateBounded<(TextRect[] Texts, FilterContext Context)>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest }, request => request.Context.SoftwareBitmap.Dispose());
    private readonly Channel<Func<Task>> uiRequests = Channel.CreateUnbounded<Func<Task>>();
    private IDisposable? captureLoop;
    private readonly IDisposable translationLoop;
    private readonly IDisposable uiLoop;
    private readonly IOcrModule ocr;
    private readonly List<PriorityRect> priorityRects;
    private readonly IOcrTextTracker ocrTextTracker;
    private readonly ITranslateModule translator;
    private readonly ICacheModule cache;
    private readonly IColorModule color;
    private readonly IEnumerable<IFilterModule> filters;
    private readonly ILogger logger;
    private readonly SemaphoreSlim analyzing = new(1, 1);
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

    private bool isFirstCapture;
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
        this.capture.Captured += Capture_CapturedAsync;
        this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        this.priorityRects = ocrParam.Value.PriorityRects ?? [];
        this.ocrTextTracker = ocrTextTracker ?? throw new ArgumentNullException(nameof(ocrTextTracker));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.color = color ?? throw new ArgumentNullException(nameof(color));
        this.filters = filters.ToArray();
        this.logger = logger;
        this.uiLoop = Observable.FromAsync(RunUiLoopAsync)
            .SubscribeOn(new DispatcherSynchronizationContext(Application.Current.Dispatcher))
            .Subscribe();
        this.translationLoop = Observable.FromAsync(RunTranslationLoopAsync, configureAwait: false)
            .SubscribeOnThreadPool()
            .Subscribe();
        if (!this.isOneShotMode)
        {
            StartCapture();
        }
        var transAsm = this.translator.GetType().Assembly;
        this.title = $"{this.name} - {this.translator.Name} ({transAsm.GetName().Version})";
    }

    partial void OnOverlayVisibleChanged(bool value)
    {
        if (value)
        {
            ApplyOnUi(this.OcrTexts.Clear);
            if (this.isOneShotMode)
            {
                this.isFirstCapture = true;
            }
            else
            {
                this.ocrTextTracker.Reset();
            }
            // Start capture when overlay becomes visible
            StartCapture();
        }
        else
        {
            // Stop capture when overlay becomes hidden
            this.captureLoop?.Dispose();
            this.capture.StopCapture();
            this.logger.LogDebug("Overlay became hidden - capture stopped");
        }
    }

    private void StartCapture()
    {
        this.captureLoop?.Dispose();
        this.captureLoop = Observable.FromAsync(RunCaptureLoopAsync, configureAwait: false)
            .SubscribeOnThreadPool()
            .Subscribe();
        this.capture.StartCapture(this.processInfoStore.MainWindowHandle);
    }

    private async ValueTask RunCaptureLoopAsync(CancellationToken cancellationToken)
    {
        var previousSize = default(System.Drawing.Size);
        await foreach (var bitmap in this.captureRequests.Reader.ReadAllAsync(cancellationToken))
        {
            var size = new System.Drawing.Size(bitmap.PixelWidth, bitmap.PixelHeight);
            if (!this.isOneShotMode && size != previousSize)
            {
                this.ocrTextTracker.Reset();
            }
            previousSize = size;
            if (await ProcessCaptureAsync(bitmap, cancellationToken) && !this.isOneShotMode && this.captureInterval > TimeSpan.Zero)
            {
                await Observable.Timer(this.captureInterval, CancellationToken.None).WaitAsync(cancellationToken);
            }
        }
    }

    private async ValueTask RunTranslationLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in this.translationRequests.Reader.ReadAllAsync(cancellationToken))
        {
            using var bitmap = request.Context.SoftwareBitmap;
            cancellationToken.ThrowIfCancellationRequested();
            await TranslateAsync(request.Texts);
            await this.analyzing.WaitAsync(cancellationToken);
            using var rel = new DisposeAction(() => this.analyzing.Release());
            using var busy = EnterBusy(this.Filtering);
            var displayedTexts = await CreateDisplayedTextsAsync(request.Texts, request.Context, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyOnUi(() => UpdateOcrTexts(displayedTexts));
        }
    }

    private async ValueTask RunUiLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var apply in this.uiRequests.Reader.ReadAllAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await apply();
        }
    }

    private void ApplyOnUi(Action apply)
    {
        this.uiRequests.Writer.TryWrite(() =>
        {
            apply();
            return Task.CompletedTask;
        });
    }

    private DisposeAction EnterBusy(BusyScope scope)
    {
        ApplyOnUi(() => scope.IsBusy = true);
        return new DisposeAction(() => ApplyOnUi(() => scope.IsBusy = false));
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

        if (this.analyzing.CurrentCount == 0)
        {
            return;
        }
        // フレームの寿命はイベント処理中だけなので、コピーの完了まで待つ。
        var bitmap = await Task.Run(async () => await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface)).ConfigureAwait(false);
        var pixelWidth = bitmap.PixelWidth;
        var pixelHeight = bitmap.PixelHeight;
        ApplyOnUi(() =>
        {
            this.Width = pixelWidth;
            this.Height = pixelHeight;
        });
        if (!this.captureRequests.Writer.TryWrite(bitmap))
        {
            bitmap.Dispose();
        }
    }

    private async Task<bool> ProcessCaptureAsync(SoftwareBitmap capturedBitmap, CancellationToken cancellationToken)
    {
        SoftwareBitmap? sbmp = capturedBitmap;
        using var bitmap = new DisposeAction(() => sbmp?.Dispose());
        await this.analyzing.WaitAsync(cancellationToken);
        using var to = this.logger.LogDebugTime("TextOverlay");
        using var rel = new DisposeAction(() =>
        {
            this.analyzing.Release();
        });
        IEnumerable<TextRect> texts;
        using (EnterBusy(this.Recognizing))
        {
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
                texts = this.isOneShotMode
                    ? observations
                    : this.ocrTextTracker.Update(observations, new(sbmp.PixelWidth, sbmp.PixelHeight));
            }
            catch (ObjectDisposedException)
            {
                // すでに破棄されている場合は何もしない
                this.captureLoop?.Dispose();
                ApplyOnUi(this.capture.StopCapture);
                return false;
            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合は何もしない
                this.captureLoop?.Dispose();
                ApplyOnUi(this.capture.StopCapture);
                return false;
            }
            catch (Exception e)
            {
                this.captureLoop?.Dispose();
                ApplyOnUi(this.capture.StopCapture);
                var path = Path.Combine(PathUtility.UserDir, $"ocr_error", $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}.png");
                await sbmp.TrySaveImage(path);
                this.uiRequests.Writer.TryWrite(async () =>
                {
                    await this.presentationService.OpenErrorDialogAsync(Resources.FaildOcr, e, this.name, path);
                    StrongReferenceMessenger.Default.Send<CloseMessage>(new(this));
                });
                return false;
            }
        }
        texts = texts.Select(t => t with { FontSize = t.FontSize * this.fontScale });

        // フィルター&翻訳処理は必ず通す
        FilterContext context;
        TextRect[] displayedTexts;
        using (EnterBusy(this.Filtering))
        {
            texts = await this.color.ConvertColorAsync(sbmp, texts);
            context = new()
            {
                SoftwareBitmap = sbmp,
                ImageSize = new(sbmp.PixelWidth, sbmp.PixelHeight),
            };
            {
                var tmp = texts.ToAsyncEnumerable();
                foreach (var filter in this.filters.OrderByDescending(f => f.Priority))
                {
                    tmp = filter.ExecutePreTranslate(tmp, context);
                }
                using var t = this.logger.LogDebugTime("PreTranslate");
                texts = await tmp.ToArrayAsync(cancellationToken);
            }
            displayedTexts = await CreateDisplayedTextsAsync(texts, context, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        ApplyOnUi(() => UpdateOcrTexts(displayedTexts));
        if (this.translationRequests.Writer.TryWrite((texts.ToArray(), context)))
        {
            sbmp = null;
        }
        return true;
    }

    private async Task<TextRect[]> CreateDisplayedTextsAsync(IEnumerable<TextRect> texts, FilterContext context, CancellationToken cancellationToken)
    {
        texts = texts.Select(t => t switch
        {
            { TranslatedText: null } when this.cache.Contains(t.SourceText) => t with { TranslatedText = this.cache.Get(t.SourceText) },
            _ => t,
        }).ToArray();
        var tmp = texts.ToAsyncEnumerable();
        foreach (var filter in this.filters.OrderBy(f => f.Priority))
        {
            tmp = filter.ExecutePostTranslate(tmp, context);
        }
        using var t = this.logger.LogDebugTime("PostTranslate");
        texts = await tmp.ToArrayAsync(cancellationToken);
        return texts.Select(t => t with { Background = Color.FromArgb((int)(255 * this.overlayOpacity), t.Background) }).ToArray();
    }

    private void UpdateOcrTexts(IEnumerable<TextRect> texts)
    {
        var hash = texts.ToHashSet();
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

    private async Task TranslateAsync(IEnumerable<TextRect> texts)
    {
        try
        {
            var requests = texts
                .Where(t => t.TranslatedText is null)
                .Where(t => !this.cache.Contains(t.SourceText))
                .ToArray();
            if (!requests.Any())
            {
                this.logger.LogDebug("翻訳キューに未翻訳がないので翻訳処理終了");
                return;
            }
            if (this.disposedValue)
            {
                this.logger.LogDebug("すでに破棄されているので翻訳キューを無視");
                return;
            }
            this.logger.LogDebug("Translate");
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
            this.captureLoop?.Dispose();
            ApplyOnUi(this.capture.StopCapture);
            this.translationLoop.Dispose();
            this.uiRequests.Writer.TryWrite(async () =>
            {
                await this.presentationService.OpenErrorDialogAsync(Resources.FaildOverlay, e, this.name, string.Empty);
                StrongReferenceMessenger.Default.Send<CloseMessage>(new(this));
            });
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
            if (this.capture is IDisposable captureDisposable)
            {
                captureDisposable.Dispose();
            }
            this.captureLoop?.Dispose();
            this.translationLoop.Dispose();
            this.uiLoop.Dispose();
            this.captureRequests.Writer.TryComplete();
            this.translationRequests.Writer.TryComplete();
            this.uiRequests.Writer.TryComplete();
            while (this.captureRequests.Reader.TryRead(out var bitmap))
            {
                bitmap.Dispose();
            }
            while (this.translationRequests.Reader.TryRead(out var request))
            {
                request.Context.SoftwareBitmap.Dispose();
            }
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
    [Inject] IOptionsSnapshot<TargetSettings> options,
    [Inject] IProcessInfoStore processInfoStore,
    [Inject] ICaptureModule capture,
    [Inject] IOcrModule ocr,
    [Inject] IOptionsSnapshot<BasicOcrParam> ocrParam,
    [Inject] IOcrTextTracker ocrTextTracker,
    [Inject] ITranslateModule translator,
    [Inject] ICacheModule cache,
    [Inject] IColorModule color,
    [Inject] IEnumerable<IFilterModule> filters,
    [Inject] ILogger<CaptureMainViewModel> logger)
    : MainViewModelBase(presentationService, options, processInfoStore, capture, ocr, ocrParam, ocrTextTracker, translator, cache, color, filters, logger)
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
    [Inject] ITranslateModule translator,
    [Inject] ICacheModule cache,
    [Inject] IColorModule color,
    [Inject] IEnumerable<IFilterModule> filters,
    [Inject] ILogger<OverlayMainViewModel> logger)
    : MainViewModelBase(presentationService, options, processInfoStore, capture, ocr, ocrParam, ocrTextTracker, translator, cache, color, filters, logger)
{
}
