using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Kamishibai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using Windows.Graphics.Imaging;
using WindowTranslator.ComponentModel;
using WindowTranslator.Extensions;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Stores;
using MessageBoxImage = Kamishibai.MessageBoxImage;

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
    private readonly SemaphoreSlim translating = new(1, 1);
    private readonly string name;
    private readonly IPresentationService presentationService;
    private readonly ICaptureModule capture;
    private readonly double fontScale;
    private TextRect[]? lastRequested;

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private double width = double.NaN;
    [ObservableProperty]
    private double height = double.NaN;

    [ObservableProperty]
    private bool isFirstBusy = true;

    private SoftwareBitmap? capturedBmp;
    private SoftwareBitmap? analyzingBmp;
    private bool disposedValue;

    public ObservableCollection<TextRect> OcrTexts { get; } = [];
    public string Font { get; }

    public MainViewModelBase(
        IPresentationService presentationService,
        IOptionsSnapshot<TargetSettings> options,
        IProcessInfoStore processInfoStore,
        ICaptureModule capture,
        IOcrModule ocr,
        ITranslateModule translator,
        ICacheModule cache,
        IColorModule color,
        IEnumerable<IFilterModule> filters,
        ILogger logger)
    {
        this.name = processInfoStore.Name;
        this.presentationService = presentationService;
        this.Font = options.Value.Font;
        this.fontScale = options.Value.FontScale;
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.capture.Captured += Capture_CapturedAsync;
        this.ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        this.translator = translator ?? throw new ArgumentNullException(nameof(translator));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
        this.color = color ?? throw new ArgumentNullException(nameof(color));
        this.filters = filters.ToArray();
        this.logger = logger;
        this.capture.StartCapture(processInfoStore.MainWindowHandle);
        this.timer = new(_ => Application.Current.Dispatcher.Invoke(() => CreateTextOverlayAsync().Forget()), null, 0, 500);
        var transAsm = this.translator.GetType().Assembly;
        this.title = $"{this.name} - {this.translator.Name} ({transAsm.GetName().Version})";
    }

    private async Task Capture_CapturedAsync(object? sender, CapturedEventArgs args)
    {
        if (this.analyzing.CurrentCount == 0)
        {
            return;
        }
        var newBmp = await SoftwareBitmap.CreateCopyFromSurfaceAsync(args.Frame.Surface);
        var sbmp = Interlocked.Exchange(ref this.capturedBmp, newBmp);
        this.Width = newBmp.PixelWidth;
        this.Height = newBmp.PixelHeight;
        CreateTextOverlayAsync().Forget();
        sbmp?.Dispose();
    }

    private async Task CreateTextOverlayAsync()
    {
        if (!await this.analyzing.WaitAsync(0))
        {
            return;
        }
        using var to = this.logger.LogDebugTime("TextOverlay");
        using var rel = new DisposeAction(() =>
        {
            this.analyzing.Release();
            this.IsFirstBusy = false;
        });
        var sbmp = Interlocked.Exchange(ref this.capturedBmp, null);
        if (sbmp is null)
        {
            sbmp = this.analyzingBmp;
        }
        else
        {
            this.analyzingBmp?.Dispose();
            this.analyzingBmp = sbmp;
        }
        if (sbmp is null)
        {
            return;
        }
        var texts = await this.ocr.RecognizeAsync(sbmp);
        {
            var tmp = texts.ToAsyncEnumerable();
            foreach (var filter in this.filters.OrderByDescending(f => f.Priority))
            {
                tmp = filter.ExecutePreTranslate(tmp);
            }
            using var t = this.logger.LogDebugTime("PreTranslate");
            texts = await tmp.ToArrayAsync();
        }
        TranslateAsync(texts).Forget();
        texts = await this.color.ConvertColorAsync(sbmp, texts);
        texts = texts.Select(t => t.IsTranslated ? t : t with { IsTranslated = this.cache.Contains(t.Text), Text = this.cache.Get(t.Text) }).ToArray();
        {
            var tmp = texts.ToAsyncEnumerable();
            foreach (var filter in this.filters.OrderBy(f => f.Priority))
            {
                tmp = filter.ExecutePostTranslate(tmp);
            }
            using var t = this.logger.LogDebugTime("PostTranslate");
            texts = await tmp.ToArrayAsync();
        }
        var hash = texts.Select(t => t with { FontSize = t.FontSize * this.fontScale }).ToHashSet();
        foreach (var item in this.OcrTexts.Where(t => !hash.Contains(t)).ToArray())
        {
            this.OcrTexts.Remove(item);
        }
        hash.ExceptWith(this.OcrTexts);
        foreach (var item in hash)
        {
            this.OcrTexts.Add(item);
        }
    }

    private async Task TranslateAsync(IEnumerable<TextRect> texts)
    {
        if (Interlocked.Exchange(ref this.lastRequested, texts.ToArray()) is not null)
        {
            this.logger.LogDebug("以前の翻訳キューを削除");
            return;
        }
        await this.translating.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Interlocked.Exchange(ref this.lastRequested, null) is not { } requests)
            {
                this.logger.LogDebug("翻訳キューがないので翻訳処理終了");
                return;
            }
            requests = requests
                .Where(t => !t.IsTranslated)
                .Where(t => !this.cache.Contains(t.Text))
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
            this.cache.AddRange(requests.Select(t => t.Text).Zip(translated));
        }
        catch (Exception e)
        {
            this.timer.DisposeAsync().Forget();
            this.capture.StopCapture();
            this.presentationService.ShowMessage(e.Message, this.name, icon: MessageBoxImage.Error);
            StrongReferenceMessenger.Default.Send<CloseMessage>(new(this));
        }
        finally
        {
            this.translating.Release();
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
    [Inject] IOptionsSnapshot<TargetSettings> options,
    [Inject] IProcessInfoStore processInfoStore,
    [Inject] ICaptureModule capture,
    [Inject] IOcrModule ocr,
    [Inject] ITranslateModule translator,
    [Inject] ICacheModule cache,
    [Inject] IColorModule color,
    [Inject] IEnumerable<IFilterModule> filters,
    [Inject] ILogger<CaptureMainViewModel> logger)
    : MainViewModelBase(presentationService, options, processInfoStore, capture, ocr, translator, cache, color, filters, logger)
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
    [Inject] ITranslateModule translator,
    [Inject] ICacheModule cache,
    [Inject] IColorModule color,
    [Inject] IEnumerable<IFilterModule> filters,
    [Inject] ILogger<OverlayMainViewModel> logger)
    : MainViewModelBase(presentationService, options, processInfoStore, capture, ocr, translator, cache, color, filters, logger)
{
}
