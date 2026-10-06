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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WindowTranslator.Modules.Main;

[ObservableObject]
public abstract partial class MainViewModelBase : IDisposable
{
    private readonly Subject<Unit> captureRequests = new();
    private readonly Subject<TextRect[]> translationRequests = new();
    private IDisposable? captureLoop;
    private readonly IDisposable translationLoop;
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
    private (TextRect[] Texts, FilterContext Context)? latestFrame;

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

    private SoftwareBitmap? capturedBmp;
    private SoftwareBitmap? analyzingBmp;
    private bool isFirstCapture;
    private bool disposedValue;

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
        this.translationLoop = this.translationRequests
            .ObserveOn(new DispatcherSynchronizationContext(Application.Current.Dispatcher))
            .SubscribeAwait(async (texts, ct) =>
            {
                if (!await TranslateAsync(texts))
                {
                    return;
                }
                await this.analyzing.WaitAsync(ct);
                using var rel = new DisposeAction(() => this.analyzing.Release());
                await UpdateDisplayedTextsAsync(ct);
            }, AwaitOperation.ThrottleFirstLast);
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
            this.latestFrame = null;
            this.OcrTexts.Clear();
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
        this.captureLoop = this.captureRequests
            .ObserveOn(new DispatcherSynchronizationContext(Application.Current.Dispatcher))
            .SubscribeAwait(async (_, ct) =>
            {
                await CreateTextOverlayAsync(ct);
                if (!this.isOneShotMode)
                {
                    await Observable.Timer(this.captureInterval, CancellationToken.None).WaitAsync(ct);
                    ct.ThrowIfCancellationRequested();
                    this.captureRequests.OnNext(default);
                }
            }, AwaitOperation.ThrottleFirstLast);
        this.capture.StartCapture(this.processInfoStore.MainWindowHandle);
    }

    private async Task Capture_CapturedAsync(object? sender, CapturedEventArgs args)
    {
        if (this.analyzing.CurrentCount == 0)
        {
            return;
        }
        if (this.isOneShotMode)
        {
            if (!this.isFirstCapture)
            {
                return;
            }
            this.isFirstCapture = false;
            this.capture.StopCapture();
        }
        var newBmp = await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface);
        var sbmp = Interlocked.Exchange(ref this.capturedBmp, newBmp);
        this.Width = newBmp.PixelWidth;
        this.Height = newBmp.PixelHeight;
        if (!this.captureRequests.IsDisposed)
        {
            this.captureRequests.OnNext(default);
        }
        sbmp?.Dispose();
    }

    private async Task CreateTextOverlayAsync(CancellationToken cancellationToken = default)
    {
        await this.analyzing.WaitAsync(cancellationToken);
        using var to = this.logger.LogDebugTime("TextOverlay");
        using var rel = new DisposeAction(() =>
        {
            this.analyzing.Release();
        });
        var sbmp = Interlocked.Exchange(ref this.capturedBmp, null);
        if (sbmp is null)
        {
            sbmp = this.analyzingBmp;
        }
        else
        {
            this.latestFrame = null;
            if (this.analyzingBmp is { } previousBmp)
            {
                if (!this.isOneShotMode
                    && (previousBmp.PixelWidth != sbmp.PixelWidth || previousBmp.PixelHeight != sbmp.PixelHeight))
                {
                    this.ocrTextTracker.Reset();
                }
                previousBmp.Dispose();
            }
            this.analyzingBmp = sbmp;
        }
        if (sbmp is null)
        {
            return;
        }

        IEnumerable<TextRect> texts;
        using (this.Recognizing.EnterBusy())
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
                this.capture.StopCapture();
                return;
            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合は何もしない
                this.captureLoop?.Dispose();
                this.capture.StopCapture();
                return;
            }
            catch (Exception e)
            {
                this.captureLoop?.Dispose();
                this.capture.StopCapture();
                var path = Path.Combine(PathUtility.UserDir, $"ocr_error", $"{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}.png");
                await sbmp.TrySaveImage(path);
                await this.presentationService.OpenErrorDialogAsync(Resources.FaildOcr, e, this.name, path);
                StrongReferenceMessenger.Default.Send<CloseMessage>(new(this));
                return;
            }
        }
        texts = texts.Select(t => t with { FontSize = t.FontSize * this.fontScale });

        // 翻訳前フィルターを通した結果を表示と翻訳へ渡す
        FilterContext context;
        using (this.Filtering.EnterBusy())
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
        }
        cancellationToken.ThrowIfCancellationRequested();
        this.latestFrame = (texts.ToArray(), context);
        this.translationRequests.OnNext(this.latestFrame.Value.Texts);
        await UpdateDisplayedTextsAsync(cancellationToken);
    }

    private async Task UpdateDisplayedTextsAsync(CancellationToken cancellationToken)
    {
        if (this.latestFrame is not { } frame)
        {
            return;
        }
        using var busy = this.Filtering.EnterBusy();
        var texts = frame.Texts.Select(t => t switch
        {
            { TranslatedText: null } when this.cache.Contains(t.SourceText) => t with { TranslatedText = this.cache.Get(t.SourceText) },
            _ => t,
        }).ToArray();
        var tmp = texts.ToAsyncEnumerable();
        foreach (var filter in this.filters.OrderBy(f => f.Priority))
        {
            tmp = filter.ExecutePostTranslate(tmp, frame.Context);
        }
        using var t = this.logger.LogDebugTime("PostTranslate");
        texts = await tmp.ToArrayAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        UpdateOcrTexts(texts.Select(t => t with { Background = Color.FromArgb((int)(255 * this.overlayOpacity), t.Background) }));
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

    private async Task<bool> TranslateAsync(IEnumerable<TextRect> texts)
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
                return false;
            }
            if (this.disposedValue)
            {
                this.logger.LogDebug("すでに破棄されているので翻訳キューを無視");
                return false;
            }
            this.logger.LogDebug("Translate");
            var translated = await this.translator.TranslateAsync(requests).ConfigureAwait(false);
            this.cache.AddRange(requests.Select(t => t.SourceText).Zip(translated));
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            this.logger.LogError(e, "翻訳中にエラーが発生");
            this.captureLoop?.Dispose();
            this.capture.StopCapture();
            this.translationLoop.Dispose();
            await Application.Current.Dispatcher.BeginInvoke(async () => await this.presentationService.OpenErrorDialogAsync(Resources.FaildOverlay, e, this.name, string.Empty));
            StrongReferenceMessenger.Default.Send<CloseMessage>(new(this));
            return false;
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
            this.captureRequests.Dispose();
            this.translationRequests.Dispose();
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
