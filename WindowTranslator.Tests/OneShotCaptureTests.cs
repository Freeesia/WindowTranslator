using System.Collections.Concurrent;
using System.Drawing;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Stores;

namespace WindowTranslator.Tests;

public class OneShotCaptureTests
{
    private static readonly TextRect first = new("Previous text", 25, 30, 120, 25, 20, false)
    {
        Context = "previous",
    };

    private static readonly TextRect second = first with
    {
        SourceText = "Current text",
        X = 27,
        Y = 32,
        FontSize = 23,
        Context = "current",
    };

    [Fact]
    public async Task ワンショットでは新しい観測をそのまま表示して翻訳する()
    {
        await using var fixture = new Fixture(isOneShotMode: true, first);

        fixture.Capture(first);
        var firstPreview = await fixture.WaitForUiAsync(IsPreviewOf(first));
        AssertRect(first, firstPreview);
        var firstCall = await fixture.Translator.NextAsync();
        Assert.Equal(first.SourceText, Assert.Single(firstCall.Texts).SourceText);
        firstCall.Complete();
        await fixture.WaitForUiAsync(IsTranslationOf(first));

        fixture.Capture(second);
        var secondPreview = await fixture.WaitForUiAsync(IsPreviewOf(second));
        AssertRect(second, secondPreview);
        var secondCall = await fixture.Translator.NextAsync();
        var translatedInput = Assert.IsType<TextRect>(Assert.Single(secondCall.Texts));
        Assert.Equal(second.SourceText, translatedInput.SourceText);
        Assert.Equal(second.Context, translatedInput.Context);
        Assert.Equal(second.X, translatedInput.X);
        Assert.Equal(second.Y, translatedInput.Y);
        Assert.Equal(second.FontSize * 1.5, translatedInput.FontSize);
        secondCall.Complete();
        var translated = await fixture.WaitForUiAsync(IsTranslationOf(second));
        AssertRect(second, translated);

        await Task.Delay(350);
        Assert.Equal(0, fixture.Tracker.UpdateCount);
        Assert.Equal(2, fixture.Color.CallCount);
        Assert.Equal(2, fixture.Translator.CallCount);
    }

    [Fact]
    public async Task ワンショットで空の観測が届いたら前回の文字列を残さない()
    {
        await using var fixture = new Fixture(isOneShotMode: true, first);

        fixture.Capture(first);
        var call = await fixture.Translator.NextAsync();
        call.Complete();
        await fixture.WaitForUiAsync(IsTranslationOf(first));
        await fixture.Filter.NextPostAsync();

        fixture.Capture();
        Assert.Empty(await fixture.Filter.NextPostAsync());
        await fixture.WaitForUiAsync(texts => texts.Length == 0);

        Assert.Equal(0, fixture.Tracker.UpdateCount);
        Assert.Equal(1, fixture.Translator.CallCount);
    }

    [Fact]
    public async Task 同じ文字列のワンショットでも最新の位置とキャッシュ済み翻訳を使う()
    {
        await using var fixture = new Fixture(isOneShotMode: true, first);
        var moved = first with { X = 28, Y = 34, FontSize = 22 };

        fixture.Capture(first);
        var call = await fixture.Translator.NextAsync();
        call.Complete();
        await fixture.WaitForUiAsync(IsTranslationOf(first));

        fixture.Capture(moved);
        var current = await fixture.WaitForUiAsync(texts =>
            IsTranslationOf(moved)(texts) && texts[0].X == moved.X && texts[0].Y == moved.Y);
        AssertRect(moved, current);
        Assert.Equal(0, fixture.Tracker.UpdateCount);
        Assert.Equal(1, fixture.Translator.CallCount);
    }

    [Fact]
    public async Task 前回のOCRが完了する前に新しいワンショットが届いたら前回の結果を使わない()
    {
        await using var fixture = new Fixture(isOneShotMode: true, first);

        var previous = fixture.CapturePending(first);
        await previous.Started.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Capture(second);
        previous.Complete();

        await fixture.WaitForUiAsync(IsPreviewOf(second));
        var current = await fixture.Translator.NextAsync();
        Assert.Equal(second.SourceText, Assert.Single(current.Texts).SourceText);
        current.Complete();
        await fixture.WaitForUiAsync(IsTranslationOf(second));

        Assert.DoesNotContain(fixture.UiHistory, texts => texts.Any(text => text.SourceText == first.SourceText));
        Assert.Equal(1, fixture.Translator.CallCount);
        Assert.Equal(0, fixture.Tracker.UpdateCount);
    }

    [Fact]
    public async Task 前回の翻訳前処理中に新しいワンショットが届いたら前回の翻訳を開始しない()
    {
        await using var fixture = new Fixture(isOneShotMode: true, first);
        var preTranslate = fixture.Filter.PauseNextPreTranslate();

        fixture.Capture(first);
        await preTranslate.Started.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Capture(second);
        await fixture.WaitForUiAsync(IsPreviewOf(second));
        preTranslate.Release();

        var current = await fixture.Translator.NextAsync();
        Assert.Equal(second.SourceText, Assert.Single(current.Texts).SourceText);
        current.Complete();
        await fixture.WaitForUiAsync(IsTranslationOf(second));
        Assert.Equal(1, fixture.Translator.CallCount);
    }

    [Fact]
    public async Task 新しいワンショットの後に前回の翻訳が完了しても前回の文字列を表示しない()
    {
        await using var fixture = new Fixture(isOneShotMode: true, first);

        fixture.Capture(first);
        await fixture.WaitForUiAsync(IsPreviewOf(first));
        var firstCall = await fixture.Translator.NextAsync();

        fixture.ViewModel.OverlayVisible = false;
        fixture.ViewModel.OverlayVisible = true;
        fixture.Capture(second);
        await fixture.WaitForUiAsync(IsPreviewOf(second));
        var historyIndex = fixture.UiHistory.Count;

        firstCall.Complete();
        var secondCall = await fixture.Translator.NextAsync();
        Assert.Equal(second.SourceText, Assert.Single(secondCall.Texts).SourceText);
        secondCall.Complete();
        await fixture.WaitForUiAsync(IsTranslationOf(second));

        Assert.DoesNotContain(fixture.UiHistory.ToArray().Skip(historyIndex),
            texts => texts.Any(text => text.SourceText == first.SourceText));
    }

    [Fact]
    public async Task UIの更新待ちに新しいワンショットが届いたら前回の表示を破棄する()
    {
        var uiContext = new QueuedSynchronizationContext();
        await using var fixture = new Fixture(isOneShotMode: true, first, uiContext);

        fixture.Capture(first);
        var previous = await fixture.Translator.NextAsync();
        fixture.Capture(second);
        previous.Complete();
        var current = await fixture.Translator.NextAsync();

        uiContext.RunPending();
        var preview = await fixture.WaitForUiAsync(IsPreviewOf(second));
        AssertRect(second, preview);
        Assert.DoesNotContain(fixture.UiHistory, texts => texts.Any(text => text.SourceText == first.SourceText));
        current.Complete();
    }

    [Fact]
    public async Task 通常モードでは追跡結果を表示して翻訳する()
    {
        await using var fixture = new Fixture(isOneShotMode: false, first);

        fixture.Capture(second);
        var preview = await fixture.WaitForUiAsync(IsPreviewOf(first));
        AssertRect(first, preview);
        var call = await fixture.Translator.NextAsync();
        Assert.Equal(first.SourceText, Assert.Single(call.Texts).SourceText);
        call.Complete();
        await fixture.WaitForUiAsync(IsTranslationOf(first));

        Assert.True(fixture.Tracker.UpdateCount > 0);
    }

    private static Func<TextRect[], bool> IsPreviewOf(TextRect text)
        => texts => texts is [{ } result] && result.SourceText == text.SourceText && result.TranslatedText is null;

    private static Func<TextRect[], bool> IsTranslationOf(TextRect text)
        => texts => texts is [{ } result]
            && result.SourceText == text.SourceText
            && result.TranslatedText == $"translated:{text.SourceText}";

    private static void AssertRect(TextRect expected, TextRect[] actual)
    {
        var text = Assert.Single(actual);
        Assert.Equal(expected.SourceText, text.SourceText);
        Assert.Equal(expected.Context, text.Context);
        Assert.Equal(expected.X, text.X);
        Assert.Equal(expected.Y, text.Y);
        Assert.Equal(expected.Width, text.Width);
        Assert.Equal(expected.Height, text.Height);
        Assert.Equal(expected.FontSize * 1.5, text.FontSize);
        Assert.Equal(Color.Cyan.ToArgb(), text.Foreground.ToArgb());
    }

    private static void AssertBitmap(SoftwareBitmap bitmap)
    {
        Assert.Equal(320, bitmap.PixelWidth);
        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        Assert.Equal(320, buffer.GetPlaneDescription(0).Width);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Channel<TextRect[]> uiUpdates = Channel.CreateUnbounded<TextRect[]>();
        private readonly OcrTraceRecorder recorder = new(NullLogger<OcrTraceRecorder>.Instance);
        private readonly TestOcr ocr = new();

        public FixedTracker Tracker { get; }
        public TestColor Color { get; } = new();
        public TestTranslator Translator { get; } = new();
        public TestFilter Filter { get; } = new();
        public TestMainViewModel ViewModel { get; }
        public ConcurrentQueue<TextRect[]> UiHistory { get; } = new();

        public Fixture(bool isOneShotMode, TextRect tracked, SynchronizationContext? uiContext = null)
        {
            this.Tracker = new FixedTracker(tracked);
            this.ViewModel = new TestMainViewModel(isOneShotMode, this.ocr, this.Tracker,
                this.recorder, this.Translator, new TestCache(), this.Color, this.Filter,
                uiContext ?? new InlineSynchronizationContext());
            this.ViewModel.OcrTexts.CollectionChanged += (_, _) =>
            {
                var texts = this.ViewModel.OcrTexts.ToArray();
                this.UiHistory.Enqueue(texts);
                this.uiUpdates.Writer.TryWrite(texts);
            };
        }

        public void Capture(params TextRect[] texts)
        {
            this.ocr.Enqueue(texts).Complete();
            this.ViewModel.EnqueueCapture(new SoftwareBitmap(BitmapPixelFormat.Bgra8, 320, 240, BitmapAlphaMode.Premultiplied));
        }

        public RecognitionCall CapturePending(params TextRect[] texts)
        {
            var call = this.ocr.Enqueue(texts);
            this.ViewModel.EnqueueCapture(new SoftwareBitmap(BitmapPixelFormat.Bgra8, 320, 240, BitmapAlphaMode.Premultiplied));
            return call;
        }

        public async Task<TextRect[]> WaitForUiAsync(Func<TextRect[], bool> predicate)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (await this.uiUpdates.Reader.WaitToReadAsync(timeout.Token))
            {
                while (this.uiUpdates.Reader.TryRead(out var texts))
                {
                    if (predicate(texts))
                    {
                        return texts;
                    }
                }
            }
            throw new InvalidOperationException("UIの更新が完了する前に終了しました。");
        }

        public async ValueTask DisposeAsync()
        {
            this.ViewModel.Dispose();
            await this.recorder.DisposeAsync();
        }
    }

    private sealed class TestMainViewModel : MainViewModelBase
    {
        public TestMainViewModel(bool isOneShotMode, IOcrModule ocr, IOcrTextTracker tracker,
            OcrTraceRecorder recorder, ITranslateModule translator, ICacheModule cache, IColorModule color,
            IFilterModule filter, SynchronizationContext uiContext)
            : base(Mock.Of<IPresentationService>(),
                new Snapshot<TargetSettings>(new() { IsOneShotMode = isOneShotMode, FontScale = 1.5, OverlayOpacity = 1 }),
                Mock.Of<IProcessInfoStore>(store => store.Name == "test"),
                Mock.Of<ICaptureModule>(), ocr, new Snapshot<BasicOcrParam>(new()), tracker,
                recorder, translator, cache, color, [filter], NullLogger.Instance, uiContext)
        {
        }
    }

    private sealed class Snapshot<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        public T Value => value;
        public T Get(string? name) => value;
    }

    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        private readonly object gate = new();

        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (this.gate)
            {
                callback(state);
            }
        }
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> callbacks = new();

        public override void Post(SendOrPostCallback callback, object? state) => this.callbacks.Enqueue((callback, state));

        public void RunPending()
        {
            while (this.callbacks.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }

    private sealed class TestOcr : IOcrModule
    {
        private readonly Channel<RecognitionCall> frames = Channel.CreateUnbounded<RecognitionCall>();

        public RecognitionCall Enqueue(TextRect[] texts)
        {
            var call = new RecognitionCall(texts);
            this.frames.Writer.TryWrite(call);
            return call;
        }

        public async ValueTask<IReadOnlyList<TextRect>> RecognizeAsync(OcrCaptureInput input)
        {
            var call = await this.frames.Reader.ReadAsync();
            call.Start();
            return await call.Result;
        }
    }

    private sealed class RecognitionCall(TextRect[] texts)
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<IReadOnlyList<TextRect>> result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => this.started.Task;
        public Task<IReadOnlyList<TextRect>> Result => this.result.Task;
        public void Start() => this.started.SetResult();
        public void Complete() => this.result.SetResult(texts);
    }

    private sealed class FixedTracker(TextRect tracked) : IOcrTextTracker
    {
        private int updateCount;

        public int UpdateCount => Volatile.Read(ref this.updateCount);

        public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize, TimeSpan? timestamp = null)
        {
            Interlocked.Increment(ref this.updateCount);
            return [tracked];
        }

        public void Reset() { }
    }

    private sealed class TestColor : IColorModule
    {
        private int callCount;

        public int CallCount => Volatile.Read(ref this.callCount);

        public ValueTask<IEnumerable<TextRect>> ConvertColorAsync(SoftwareBitmap bitmap, IEnumerable<TextRect> texts)
        {
            AssertBitmap(bitmap);
            Interlocked.Increment(ref this.callCount);
            return ValueTask.FromResult<IEnumerable<TextRect>>(
                texts.Select(text => text with { Foreground = Color.Cyan }).ToArray());
        }
    }

    private sealed class TestCache : ICacheModule
    {
        private readonly ConcurrentDictionary<string, string> values = new();

        public bool Contains(string src) => this.values.ContainsKey(src);
        public string Get(string src) => this.values[src];
        public void AddRange(IEnumerable<(string src, string dst)> pairs)
        {
            foreach (var (src, dst) in pairs)
            {
                this.values[src] = dst;
            }
        }
    }

    private sealed class TestTranslator : ITranslateModule
    {
        private readonly Channel<TranslationCall> calls = Channel.CreateUnbounded<TranslationCall>();
        private int callCount;

        public int CallCount => Volatile.Read(ref this.callCount);

        public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        {
            var call = new TranslationCall(srcTexts);
            Interlocked.Increment(ref this.callCount);
            this.calls.Writer.TryWrite(call);
            return new(call.Result);
        }

        public async Task<TranslationCall> NextAsync()
            => await this.calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class TranslationCall(TextInfo[] texts)
    {
        private readonly TaskCompletionSource<string[]> result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TextInfo[] Texts => texts;
        public Task<string[]> Result => this.result.Task;
        public void Complete() => this.result.SetResult(texts.Select(text => $"translated:{text.SourceText}").ToArray());
    }

    private sealed class TestFilter : IFilterModule
    {
        private readonly Channel<TextRect[]> postCalls = Channel.CreateUnbounded<TextRect[]>();
        private ProcessingGate? nextPreTranslate;

        public ProcessingGate PauseNextPreTranslate()
        {
            var gate = new ProcessingGate();
            this.nextPreTranslate = gate;
            return gate;
        }

        public IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
        {
            Assert.Equal(new Size(320, 240), context.ImageSize);
            AssertBitmap(context.SoftwareBitmap);
            var gate = Interlocked.Exchange(ref this.nextPreTranslate, null);
            return gate is null ? texts : WaitAsync(texts, gate);
        }

        public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
        {
            AssertBitmap(context.SoftwareBitmap);
            return RecordAsync(texts, this.postCalls.Writer);
        }

        public async Task<TextRect[]> NextPostAsync()
            => await this.postCalls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        private static async IAsyncEnumerable<TextRect> WaitAsync(IAsyncEnumerable<TextRect> texts, ProcessingGate gate)
        {
            gate.Start();
            await gate.WaitAsync().ConfigureAwait(false);
            await foreach (var text in texts)
            {
                yield return text;
            }
        }

        private static async IAsyncEnumerable<TextRect> RecordAsync(
            IAsyncEnumerable<TextRect> texts, ChannelWriter<TextRect[]> writer)
        {
            var result = await texts.ToArrayAsync();
            writer.TryWrite(result);
            foreach (var text in result)
            {
                yield return text;
            }
        }

        public sealed class ProcessingGate
        {
            private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Started => this.started.Task;
            public Task WaitAsync() => this.released.Task.WaitAsync(TimeSpan.FromSeconds(5));
            public void Start() => this.started.SetResult();
            public void Release() => this.released.SetResult();
        }
    }
}
