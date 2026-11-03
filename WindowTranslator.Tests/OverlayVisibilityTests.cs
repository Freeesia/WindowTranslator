using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using R3;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Stores;

namespace WindowTranslator.Tests;

public sealed class OverlayVisibilityTests
{
    private const string ChildProcessVariable = "WINDOW_TRANSLATOR_OVERLAY_TEST_CHILD";

    [Fact]
    public async Task 非表示中に追跡を止めて再表示後は新しい認識結果だけを表示する()
    {
        if (Environment.GetEnvironmentVariable(ChildProcessVariable) == "1")
        {
            await RunInStaAsync();
            return;
        }

        // WPF の Application は同一プロセスで再作成できないため、既存テストと分離する。
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(OverlayVisibilityTests).Assembly.Location);
        start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName~WindowTranslator.Tests.OverlayVisibilityTests");
        start.Environment[ChildProcessVariable] = "1";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("テストプロセスを起動できません。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        Assert.True(process.ExitCode == 0, $"{await output}\n{await error}");
    }

    private static async Task RunInStaAsync()
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                Exception? failure = null;
                Fixture? afterShutdown = null;
                SoftwareBitmap? pendingAfterShutdown = null;
                _ = application.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await VerifyHiddenAsync();
                        foreach (var stage in Enum.GetValues<Stage>())
                        {
                            await VerifyInFlightAsync(stage);
                        }
                        await VerifyInFlightAsync(Stage.Ocr, withoutContext: true);
                        await VerifyInFlightAsync(Stage.Ocr, failFirst: true);
                        await VerifyInFlightAsync(Stage.Translation, failFirst: true);
                        await VerifyHiddenBeforeWorkStartsAsync();
                        await VerifyHiddenDuringResetAsync();
                        await VerifyHiddenDuringTrackingAsync();
                        await VerifyHiddenDuringColorAsync();
                        await VerifyHiddenBeforeTranslationAsync();
                        await VerifyOldEmptyResultDoesNotRemoveNewAsync();
                        await VerifyUiNotificationsAsync();
                        await VerifyReentrantShowAsync();
                        await VerifyReentrantHideAsync();
                        await VerifyNestedVisibilityAsync();
                        await VerifyPendingAsync();
                        await VerifyOneShotAsync();
                        await VerifyDelayedCaptureAsync();
                        await VerifyDisposeAsync();
                        afterShutdown = new Fixture(captureInterval: 0.5);
                        pendingAfterShutdown = Bitmap(320);
                        afterShutdown.SubmitCapture(pendingAfterShutdown);
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                    finally
                    {
                        application.Shutdown();
                    }
                });
                application.Run();
                if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
                Task.Run(afterShutdown!.Model.Dispose).GetAwaiter().GetResult();
                Assert.True(IsDisposed(pendingAfterShutdown!));
                afterShutdown.DisposeAsync().AsTask().GetAwaiter().GetResult();
                finished.TrySetResult();
            }
            catch (Exception exception)
            {
                finished.TrySetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    private static async Task VerifyHiddenAsync()
    {
        await using var fixture = new Fixture();
        SoftwareBitmap? tracked = null;
        var hiddenTranslations = 0;
        using var tracking = fixture.Tracking.Subscribe(request =>
        {
            if (request.Context is { } context) tracked = context.SoftwareBitmap;
        });
        using var translations = fixture.Translations.Subscribe(request =>
        {
            if (request is not null && !fixture.Model.OverlayVisible) hiddenTranslations++;
        });
        fixture.SubmitCapture(Bitmap(320));
        await WaitUntilAsync(() => fixture.Translator.Gate.Count > 0 && tracked is not null);
        fixture.Model.OverlayVisible = false;
        Assert.False(fixture.TrackingTimer.IsEnabled);
        var trackerCount = fixture.Tracker.UpdateCount;
        var colorCount = fixture.Color.Gate.Count;
        await Task.Delay(400);

        Assert.Equal(trackerCount, fixture.Tracker.UpdateCount);
        Assert.Equal(colorCount, fixture.Color.Gate.Count);
        Assert.Equal(0, hiddenTranslations);
        Assert.True(IsDisposed(tracked!));
        fixture.Model.OverlayVisible = true;
        Assert.Empty(fixture.Model.OcrTexts);
        fixture.SubmitCapture(Bitmap(321));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
        Assert.DoesNotContain(fixture.Model.OcrTexts, text => text.SourceText == "old");
        Assert.True(fixture.Tracker.ResetCount >= 2);
    }

    private static async Task VerifyInFlightAsync(Stage stage, bool withoutContext = false, bool failFirst = false)
    {
        await using var fixture = new Fixture(stage, withoutContext: withoutContext, failFirst: failFirst);
        var gate = stage switch
        {
            Stage.Ocr => fixture.Ocr.Gate,
            Stage.Color => fixture.Color.Gate,
            _ => fixture.Translator.Gate,
        };
        var shown = new List<string>();
        fixture.Model.OcrTexts.CollectionChanged += (_, _) =>
            shown.AddRange(fixture.Model.OcrTexts.Select(text => text.SourceText));
        fixture.SubmitCapture(Bitmap(320));
        await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Model.OverlayVisible = false;
        fixture.Model.OverlayVisible = true;
        shown.Clear();
        fixture.SubmitCapture(Bitmap(321));
        await Task.Delay(200);
        Assert.False(gate.NextStarted.Task.IsCompleted);
        gate.Release.TrySetResult();
        await gate.NextStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
        Assert.DoesNotContain("old", shown);
    }

    private static async Task VerifyHiddenDuringResetAsync()
    {
        await using var fixture = new Fixture(delayReset: true);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Tracker.ResetStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Model.OverlayVisible = false;
        fixture.Tracker.ResetRelease.Set();
        await Task.Delay(250);
        Assert.False(fixture.Tracker.Updated.Task.IsCompleted);

        fixture.Model.OverlayVisible = true;
        fixture.SubmitCapture(Bitmap(321));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
    }

    private static async Task VerifyHiddenBeforeWorkStartsAsync()
    {
        await using var fixture = new Fixture(delayFilter: true);
        const int blockedWorkers = 4;
        using var started = new CountdownEvent(blockedWorkers);
        using var release = new ManualResetEventSlim();
        ThreadPool.GetMinThreads(out var minWorker, out var minIo);
        ThreadPool.GetMaxThreads(out var maxWorker, out var maxIo);
        Task[] blockers = [];
        try
        {
            Assert.True(ThreadPool.SetMaxThreads(Math.Max(maxWorker, blockedWorkers + 1), maxIo));
            Assert.True(ThreadPool.SetMinThreads(Math.Max(minWorker, blockedWorkers + 1), minIo));
            blockers = Enumerable.Range(0, blockedWorkers).Select(_ => Task.Run(() =>
            {
                started.Signal();
                if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException();
            })).ToArray();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(ThreadPool.SetMinThreads(1, minIo));
            Assert.True(ThreadPool.SetMaxThreads(blockedWorkers, maxIo));
            fixture.SubmitCapture(Bitmap(320));
            fixture.Tracking.OnNext(([new("old", 0, 0, 100, 30, 20, false)],
                new() { SoftwareBitmap = Bitmap(320), ImageSize = new(320, 240) }));
            fixture.Translations.OnNext(([new("old", 0, 0, 100, 30, 20, false)],
                new() { SoftwareBitmap = Bitmap(320), ImageSize = new(320, 240) }));
            var timeout = Stopwatch.StartNew();
            while (fixture.HiddenSubscriberCount != 3 && timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            }
            Assert.Equal(3, fixture.HiddenSubscriberCount);
            fixture.Model.OverlayVisible = false;
            fixture.Filter.Gate.Release.TrySetResult();
        }
        finally
        {
            release.Set();
            ThreadPool.SetMinThreads(1, minIo);
            ThreadPool.SetMaxThreads(maxWorker, maxIo);
            ThreadPool.SetMinThreads(minWorker, minIo);
            await Task.WhenAll(blockers).WaitAsync(TimeSpan.FromSeconds(5));
        }
        await WaitUntilAsync(() => fixture.HiddenSubscriberCount == 0);
        Assert.False(fixture.Ocr.Gate.Started.Task.IsCompleted);
        Assert.False(fixture.Tracker.ResetStarted.Task.IsCompleted);
        Assert.False(fixture.Filter.Gate.Started.Task.IsCompleted);
    }

    private static async Task VerifyHiddenDuringTrackingAsync()
    {
        await using var fixture = new Fixture(delayUpdate: true);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Tracker.Updated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Model.OverlayVisible = false;
        fixture.Tracker.UpdateRelease.Set();
        await Task.Delay(250);
        Assert.Equal(0, fixture.Color.Gate.Count);

        fixture.Model.OverlayVisible = true;
        fixture.SubmitCapture(Bitmap(321));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
    }

    private static async Task VerifyHiddenDuringColorAsync()
    {
        await using var fixture = new Fixture(Stage.Color);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Color.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Model.OverlayVisible = false;
        fixture.Color.Gate.Release.TrySetResult();
        await WaitUntilAsync(() => fixture.Color.Bitmap is { } bitmap && IsDisposed(bitmap));
        Assert.Equal(0, fixture.Cache.ContainsCount);
    }

    private static async Task VerifyHiddenBeforeTranslationAsync()
    {
        await using var fixture = new Fixture(delayFilter: true);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Filter.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pending = Bitmap(322);
        fixture.Translations.OnNext(([new("pending", 0, 0, 100, 30, 20, false)],
            new() { SoftwareBitmap = pending, ImageSize = new(322, 240) }));
        fixture.Model.OverlayVisible = false;
        Assert.True(IsDisposed(pending));
        fixture.Filter.Gate.Release.TrySetResult();
        await Task.Delay(250);
        Assert.False(fixture.Translator.Gate.Started.Task.IsCompleted);

        fixture.Model.OverlayVisible = true;
        fixture.SubmitCapture(Bitmap(321));
        await WaitUntilAsync(() => fixture.Translator.Gate.Started.Task.IsCompleted);
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
    }

    private static async Task VerifyOldEmptyResultDoesNotRemoveNewAsync()
    {
        await using var fixture = new Fixture(delayFilter: true, emptyFirstCapture: true, delayNextFilter: true);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Filter.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Model.OverlayVisible = false;
        fixture.Model.OverlayVisible = true;
        fixture.SubmitCapture(Bitmap(321));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
        fixture.Filter.Gate.Release.TrySetResult();
        await fixture.Filter.Gate.NextStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.Contains(fixture.Model.OcrTexts, text => text.SourceText == "new");
        }
        finally
        {
            fixture.Filter.Gate.NextRelease.TrySetResult();
        }
    }

    private static async Task VerifyUiNotificationsAsync()
    {
        await using var fixture = new Fixture(multipleTexts: true);
        var switched = false;
        var addedAfterShow = new List<string>();
        fixture.Model.OcrTexts.CollectionChanged += (_, change) =>
        {
            if (change.NewItems is not { } items) return;
            if (switched)
            {
                addedAfterShow.AddRange(items.Cast<TextRect>().Select(text => text.SourceText));
            }
            else
            {
                switched = true;
                fixture.Model.OverlayVisible = false;
                fixture.Model.OverlayVisible = true;
            }
        };
        fixture.SubmitCapture(Bitmap(320));
        await WaitUntilAsync(() => switched);
        await Task.Delay(100);
        Assert.Empty(addedAfterShow);
        Assert.Empty(fixture.Model.OcrTexts);
    }

    private static async Task VerifyPendingAsync()
    {
        await using (var immediate = new Fixture())
        {
            var image = Bitmap(322);
            immediate.SubmitCapture(image);
            immediate.Model.OverlayVisible = false;
            await WaitUntilAsync(() => IsDisposed(image));
        }

        await using (var slow = new Fixture(captureInterval: (uint.MaxValue - 1) / 1000d))
        {
            var pendingFrame = Bitmap(323);
            slow.SubmitCapture(pendingFrame);
            slow.Model.OverlayVisible = false;
            Assert.True(IsDisposed(pendingFrame));
        }

        await using (var delayed = new Fixture(captureInterval: 0.1))
        {
            delayed.SubmitCapture(Bitmap(320));
            Thread.Sleep(250);
            delayed.Model.OverlayVisible = false;
            delayed.Model.OverlayVisible = true;
            delayed.SubmitCapture(Bitmap(321));
            await Task.Delay(30);
            Assert.False(delayed.Ocr.Gate.Started.Task.IsCompleted);
            await WaitUntilAsync(() => delayed.Model.OcrTexts.Any(text => text.SourceText == "new"));
        }

        await using (var throttled = new Fixture(captureInterval: 0.5))
        {
            var first = Bitmap(320);
            var last = Bitmap(321);
            throttled.SubmitCapture(first);
            await Task.Delay(100);
            throttled.SubmitCapture(last);
            Assert.True(IsDisposed(first));
            await WaitUntilAsync(() => throttled.Model.OcrTexts.Any(text => text.SourceText == "new"));
            Assert.DoesNotContain(throttled.Model.OcrTexts, text => text.SourceText == "old");
            var pendingFrame = Bitmap(323);
            throttled.SubmitCapture(pendingFrame);
            throttled.Model.OverlayVisible = false;
            Assert.True(IsDisposed(pendingFrame));
            throttled.Model.OverlayVisible = true;
            throttled.SubmitCapture(Bitmap(321));
            await WaitUntilAsync(() => throttled.Model.OcrTexts.Any(text => text.SourceText == "new"));
        }

        await using var fixture = new Fixture(Stage.Ocr);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Ocr.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pending = Bitmap(322);
        fixture.SubmitCapture(pending);
        await Task.Delay(150);
        fixture.Model.OverlayVisible = false;
        Assert.True(IsDisposed(pending));
        fixture.Model.OverlayVisible = true;
        fixture.Ocr.Gate.Release.TrySetResult();
        await Task.Delay(250);
        Assert.False(fixture.Ocr.Gate.NextStarted.Task.IsCompleted);
    }

    private static async Task VerifyDelayedCaptureAsync()
    {
        await using var fixture = new Fixture();
        var handler = fixture.Capture.CurrentHandler!;
        fixture.Model.OverlayVisible = false;
        Assert.Null(fixture.Capture.CurrentHandler);
        fixture.Model.OverlayVisible = true;
        Assert.NotNull(fixture.Capture.CurrentHandler);
        Assert.NotSame(handler, fixture.Capture.CurrentHandler);
        await handler.InvokeAsync(fixture.Capture, null!);
        await Task.Delay(150);
        Assert.False(fixture.Ocr.Gate.Started.Task.IsCompleted);
    }

    private static async Task VerifyOneShotAsync()
    {
        await using var fixture = new Fixture(captureInterval: 10, isOneShotMode: true);
        fixture.SubmitCapture(Bitmap(321));
        await fixture.Ocr.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
        Assert.Equal(0, fixture.Tracker.UpdateCount);
    }

    private static async Task VerifyReentrantShowAsync()
    {
        await using var fixture = new Fixture(Stage.Ocr);
        var oldShown = false;
        fixture.Model.OcrTexts.CollectionChanged += (_, _) =>
            oldShown |= fixture.Model.OcrTexts.Any(text => text.SourceText == "old");
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Ocr.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        fixture.Capture.OnStopped = () =>
        {
            fixture.Capture.OnStopped = null;
            fixture.Model.OverlayVisible = true;
        };
        fixture.Model.OverlayVisible = false;
        fixture.SubmitCapture(Bitmap(321));
        fixture.Ocr.Gate.Release.TrySetResult();

        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
        Assert.False(oldShown);
    }

    private static async Task VerifyDisposeAsync()
    {
        await using (var pending = new Fixture(captureInterval: 0.5))
        {
            var image = Bitmap(320);
            pending.SubmitCapture(image);
            await Task.Run(pending.Model.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(IsDisposed(image));
        }

        await using (var running = new Fixture(Stage.Ocr))
        {
            var image = Bitmap(320);
            running.SubmitCapture(image);
            await running.Ocr.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Run(running.Model.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            running.Ocr.Gate.Release.TrySetResult();
            await WaitUntilAsync(() => IsDisposed(image));
        }
    }

    private static async Task VerifyReentrantHideAsync()
    {
        await using var fixture = new Fixture();
        fixture.Model.OcrTexts.Add(new TextRect("old", 0, 0, 100, 30, 20, false));
        fixture.Model.OverlayVisible = false;
        var starts = fixture.Capture.StartCount;
        fixture.Model.OcrTexts.CollectionChanged += (_, _) => fixture.Model.OverlayVisible = false;

        fixture.Model.OverlayVisible = true;

        Assert.False(fixture.Model.OverlayVisible);
        Assert.Equal(starts, fixture.Capture.StartCount);
    }

    private static async Task VerifyNestedVisibilityAsync()
    {
        await using var fixture = new Fixture();
        fixture.Model.OcrTexts.Add(new TextRect("old", 0, 0, 100, 30, 20, false));
        fixture.Model.OverlayVisible = false;
        var starts = fixture.Capture.StartCount;
        var toggled = false;
        fixture.Model.OcrTexts.CollectionChanged += (_, _) =>
        {
            if (toggled) return;
            toggled = true;
            fixture.Model.OverlayVisible = false;
            fixture.Model.OverlayVisible = true;
        };
        fixture.Model.OcrTexts.CollectionChanged += (_, _) => { };

        fixture.Model.OverlayVisible = true;
        await Task.Delay(50);

        Assert.True(fixture.Model.OverlayVisible);
        Assert.Equal(starts + 1, fixture.Capture.StartCount);
    }

    private static SoftwareBitmap Bitmap(int width) => new(BitmapPixelFormat.Bgra8, width, 240);

    private static bool IsDisposed(SoftwareBitmap bitmap)
    {
        try
        {
            using var view = bitmap.GetReadOnlyView();
            return false;
        }
        catch (Exception exception) when (exception is ObjectDisposedException || exception.HResult == unchecked((int)0x80000013))
        {
            return true;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(20);
        }
        Assert.True(condition(), "期待した処理が時間内に完了しませんでした。");
    }

    private enum Stage { Ocr, Color, Translation }

    private sealed class Fixture : System.IAsyncDisposable
    {
        private readonly OcrTraceRecorder recorder = new(NullLogger<OcrTraceRecorder>.Instance);
        private readonly Action<SoftwareBitmap> submitCapture;

        public Fixture(Stage? delayed = null, double captureInterval = 0, bool delayReset = false, bool delayFilter = false, bool multipleTexts = false, bool withoutContext = false, bool failFirst = false, bool delayUpdate = false, bool isOneShotMode = false, bool emptyFirstCapture = false, bool delayNextFilter = false)
        {
            this.Ocr = new(delayed == Stage.Ocr, multipleTexts, failFirst && delayed == Stage.Ocr, emptyFirstCapture);
            this.Color = new(delayed == Stage.Color);
            this.Translator = new(delayed == Stage.Translation, failFirst && delayed == Stage.Translation);
            this.Tracker = new(delayReset, delayUpdate);
            this.Filter = new(delayFilter, delayNextFilter);
            var context = SynchronizationContext.Current;
            try
            {
                if (withoutContext) SynchronizationContext.SetSynchronizationContext(null);
                this.Model = new OverlayMainViewModel(
                    null!, new Snapshot<TargetSettings>(new() { CaptureInterval = captureInterval, IsOneShotMode = isOneShotMode }), new ProcessInfo(),
                    this.Capture, this.Ocr, new Snapshot<BasicOcrParam>(new()), this.Tracker, this.recorder,
                    this.Translator, this.Cache, this.Color, delayFilter ? [this.Filter] : [], NullLogger<OverlayMainViewModel>.Instance);
            }
            finally
            {
                if (withoutContext) SynchronizationContext.SetSynchronizationContext(context);
            }
            this.submitCapture = typeof(MainViewModelBase).GetMethod("QueueCapture", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Action<SoftwareBitmap>>(this.Model);
        }

        public Capture Capture { get; } = new();
        public Ocr Ocr { get; }
        public Tracker Tracker { get; }
        public Color Color { get; }
        public Translator Translator { get; }
        public Cache Cache { get; } = new();
        public Filter Filter { get; }
        public OverlayMainViewModel Model { get; }
        public System.Windows.Threading.DispatcherTimer TrackingTimer => Field<System.Windows.Threading.DispatcherTimer>("trackingTimer");
        public Subject<(IEnumerable<TextRect> Texts, FilterContext Context)> Tracking
            => Field<Subject<(IEnumerable<TextRect>, FilterContext)>>("trackingRequests");
        public Subject<(IEnumerable<TextRect> Texts, FilterContext Context)?> Translations
            => Field<Subject<(IEnumerable<TextRect>, FilterContext)?>>("translationRequests");
        public int HiddenSubscriberCount
            => ((Delegate?)typeof(MainViewModelBase).GetField("OverlayHidden", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this.Model))?.GetInvocationList().Length ?? 0;

        public void SubmitCapture(SoftwareBitmap bitmap) => this.submitCapture(bitmap);

        private T Field<T>(string name) where T : class
            => (T)typeof(MainViewModelBase).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this.Model)!;

        public async ValueTask DisposeAsync()
        {
            this.Model.Dispose();
            await this.recorder.DisposeAsync();
            this.Tracker.ResetRelease.Dispose();
            this.Tracker.UpdateRelease.Dispose();
        }
    }

    private sealed class Gate(bool delayed, bool delayNext = false)
    {
        private int count;
        public int Count => Volatile.Read(ref this.count);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NextStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NextRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task EnterAsync()
        {
            var current = Interlocked.Increment(ref this.count);
            if (current == 1) this.Started.TrySetResult();
            else this.NextStarted.TrySetResult();
            if (delayed && current == 1) await this.Release.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            if (delayNext && current == 2) await this.NextRelease.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
    }

    private sealed class Snapshot<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        public T Value { get; } = value;
        public T Get(string? name) => this.Value;
    }

    private sealed class ProcessInfo : IProcessInfoStore
    {
        public IntPtr MainWindowHandle => IntPtr.Zero;
        public string Name => "test";
    }

    private sealed class Capture : ICaptureModule
    {
        public event AsyncEventHandler<CapturedEventArgs>? Captured;
        public AsyncEventHandler<CapturedEventArgs>? CurrentHandler => this.Captured;
        public Action? OnStopped { get; set; }
        public int StartCount { get; private set; }
        public void StartCapture(IntPtr targetWindow) => this.StartCount++;
        public void StopCapture() => this.OnStopped?.Invoke();
    }

    private sealed class Ocr(bool delayed, bool multipleTexts, bool failFirst, bool emptyFirstCapture) : IOcrModule
    {
        public Gate Gate { get; } = new(delayed);
        public async ValueTask<IReadOnlyList<TextRect>> RecognizeAsync(OcrCaptureInput input)
        {
            await this.Gate.EnterAsync();
            if (failFirst && this.Gate.Count == 1) throw new InvalidOperationException("模擬 OCR エラー");
            if (emptyFirstCapture && input.Source.PixelWidth == 320) return [];
            var text = input.Source.PixelWidth == 321 ? "new" : "old";
            var first = new TextRect(text, 0, 0, 100, 30, 20, false);
            return multipleTexts ? [first, first with { SourceText = "old2", Y = 40 }] : [first];
        }
    }

    private sealed class Tracker(bool delayReset, bool delayUpdate) : IOcrTextTracker
    {
        private int updates;
        private int resets;
        private TextRect[] previous = [];
        public TaskCompletionSource ResetStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ResetRelease { get; } = new();
        public ManualResetEventSlim UpdateRelease { get; } = new();
        public TaskCompletionSource Updated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int UpdateCount => Volatile.Read(ref this.updates);
        public int ResetCount => Volatile.Read(ref this.resets);
        public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, System.Drawing.Size imageSize, TimeSpan? timestamp = null)
        {
            var count = Interlocked.Increment(ref this.updates);
            this.Updated.TrySetResult();
            if (delayUpdate && count == 1 && !this.UpdateRelease.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException();
            }
            var current = observations.ToArray();
            this.previous = [.. this.previous.Where(old => current.All(text => text.SourceText != old.SourceText)), .. current];
            return this.previous;
        }
        public void Reset()
        {
            var count = Interlocked.Increment(ref this.resets);
            this.previous = [];
            this.ResetStarted.TrySetResult();
            if (delayReset && count == 1 && !this.ResetRelease.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException();
            }
        }
    }

    private sealed class Color(bool delayed) : IColorModule
    {
        public Gate Gate { get; } = new(delayed);
        public SoftwareBitmap? Bitmap { get; private set; }
        public async ValueTask<IEnumerable<TextRect>> ConvertColorAsync(SoftwareBitmap bitmap, IEnumerable<TextRect> texts)
        {
            this.Bitmap = bitmap;
            var result = texts.ToArray();
            await this.Gate.EnterAsync();
            return result;
        }
    }

    private sealed class Translator(bool delayed, bool failFirst) : ITranslateModule
    {
        public Gate Gate { get; } = new(delayed);
        public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        {
            await this.Gate.EnterAsync();
            if (failFirst && this.Gate.Count == 1) throw new InvalidOperationException("模擬翻訳エラー");
            return srcTexts.Select(text => text.SourceText).ToArray();
        }
    }

    private sealed class Filter(bool delayed, bool delayNext) : IFilterModule
    {
        public Gate Gate { get; } = new(delayed, delayNext);
        public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
        {
            await this.Gate.EnterAsync();
            await foreach (var text in texts) yield return text;
        }

        public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context) => texts;
    }

    private sealed class Cache : ICacheModule
    {
        private int containsCount;
        public int ContainsCount => Volatile.Read(ref this.containsCount);
        public bool Contains(string src)
        {
            Interlocked.Increment(ref this.containsCount);
            return false;
        }
        public string Get(string src) => throw new InvalidOperationException();
        public void AddRange(IEnumerable<(string src, string dst)> pairs) { }
    }
}
