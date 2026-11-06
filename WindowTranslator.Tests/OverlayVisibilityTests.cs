using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using R3;
using Windows.Graphics.Imaging;
using WindowTranslator.Controls;
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
                        await VerifyQueuedCapturesAsync();
                        await VerifyOcrCompletesWhileHiddenAsync();
                        await VerifyHiddenDuringResetAsync();
                        await VerifyHiddenDuringTrackingAsync();
                        await VerifyUiResponsiveDuringTrackingAsync();
                        await VerifyUiResponsiveDuringTranslationErrorAsync();
                        await VerifyHiddenDuringColorAsync();
                        await VerifyHiddenDuringCacheAsync();
                        await VerifyOldHandoffAfterReopenAsync();
                        await VerifyHiddenBeforeTranslationAsync();
                        await VerifyOldEmptyResultDoesNotRemoveNewAsync();
                        await VerifyOneShotAsync();
                        await VerifyAutomaticWindowVisibilityAsync();
                        await VerifyOneShotWindowVisibilityAsync();
                        await VerifyDelayedCaptureAsync(isOneShotMode: false);
                        await VerifyDelayedCaptureAsync(isOneShotMode: true);
                        await VerifyQueuedOneShotStopAsync();
                        await VerifyDisposeAsync();
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
        var hiddenTicks = 0;
        var hiddenTranslations = 0;
        using var tracking = fixture.Tracking.Subscribe(request =>
        {
            if (request.Context is { } context) Volatile.Write(ref tracked, context.SoftwareBitmap);
            else if (!fixture.Model.OverlayVisible)
            {
                Interlocked.Increment(ref hiddenTicks);
            }
        });
        using var translations = fixture.Translations.Subscribe(request =>
        {
            if (request is not null && !fixture.Model.OverlayVisible) Interlocked.Increment(ref hiddenTranslations);
        });
        fixture.SubmitCapture(Bitmap(320));
        await WaitUntilAsync(() => fixture.Translator.Gate.Count > 0 && Volatile.Read(ref tracked) is not null);
        fixture.Model.OverlayVisible = false;
        var trackerCount = fixture.Tracker.UpdateCount;
        var colorCount = fixture.Color.Gate.Count;
        await WaitUntilAsync(() => IsDisposed(Volatile.Read(ref tracked)!));
        await Task.Delay(150);
        var ticksAfterStopping = Volatile.Read(ref hiddenTicks);
        await Task.Delay(250);

        Assert.Equal(ticksAfterStopping, Volatile.Read(ref hiddenTicks));
        Assert.Equal(trackerCount, fixture.Tracker.UpdateCount);
        Assert.Equal(colorCount, fixture.Color.Gate.Count);
        Assert.Equal(0, Volatile.Read(ref hiddenTranslations));
        Assert.True(IsDisposed(Volatile.Read(ref tracked)!));
        fixture.Model.OverlayVisible = true;
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        Assert.Empty(fixture.Model.OcrTexts);
        fixture.Ocr.Text = "new";
        fixture.SubmitCapture(Bitmap(320));
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

    private static async Task VerifyQueuedCapturesAsync()
    {
        await using (var throttled = new Fixture(captureInterval: 0.3))
        {
            var old = Bitmap(320);
            throttled.SubmitCapture(old);
            throttled.Model.OverlayVisible = false;
            throttled.Model.OverlayVisible = true;
            await WaitUntilAsync(() => IsDisposed(old));
            Assert.False(throttled.Ocr.Gate.Started.Task.IsCompleted);
            throttled.SubmitCapture(Bitmap(321));
            await WaitUntilAsync(() => throttled.Model.OcrTexts.Any(text => text.SourceText == "new"));
        }

        await using (var queued = new Fixture(Stage.Ocr))
        {
            queued.SubmitCapture(Bitmap(320));
            await queued.Ocr.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var old = Bitmap(322);
            queued.SubmitCapture(old);
            await Task.Delay(150);
            queued.Model.OverlayVisible = false;
            queued.Model.OverlayVisible = true;
            queued.Ocr.Gate.Release.TrySetResult();
            await WaitUntilAsync(() => IsDisposed(old));
            Assert.False(queued.Ocr.Gate.NextStarted.Task.IsCompleted);
            queued.SubmitCapture(Bitmap(321));
            await WaitUntilAsync(() => queued.Model.OcrTexts.Any(text => text.SourceText == "new"));
        }
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

    private static async Task VerifyOcrCompletesWhileHiddenAsync()
    {
        await using var fixture = new Fixture(Stage.Ocr);
        SoftwareBitmap? tracked = null;
        using var tracking = fixture.Tracking.Subscribe(request =>
        {
            if (request.Context is { } context) tracked = context.SoftwareBitmap;
        });
        var image = Bitmap(320);
        fixture.SubmitCapture(image);
        await fixture.Ocr.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Model.OverlayVisible = false;
        await Task.Delay(150);
        fixture.Ocr.Gate.Release.TrySetResult();
        await WaitUntilAsync(() => IsDisposed(image));
        await Task.Delay(200);
        Assert.True(tracked is null || IsDisposed(tracked));
        Assert.Equal(0, fixture.Tracker.UpdateCount);
        Assert.Equal(0, fixture.Color.Gate.Count);
        Assert.Equal(0, fixture.Translator.Gate.Count);
    }

    private static async Task VerifyUiResponsiveDuringTrackingAsync()
    {
        await using var fixture = new Fixture(delayUpdate: true);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Tracker.Updated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var dispatcher = Application.Current.Dispatcher;
        var responsive = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250);
                var ping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = dispatcher.InvokeAsync(() => ping.TrySetResult());
                return await Task.WhenAny(ping.Task, Task.Delay(1000)) == ping.Task;
            }
            finally
            {
                fixture.Tracker.UpdateRelease.Set();
            }
        });
        fixture.Model.OverlayVisible = false;
        fixture.Model.OverlayVisible = true;
        fixture.SubmitCapture(Bitmap(321));
        Assert.True(await responsive.WaitAsync(TimeSpan.FromSeconds(5)));
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

    private static async Task VerifyUiResponsiveDuringTranslationErrorAsync()
    {
        await using var fixture = new Fixture(Stage.Translation, failFirst: true);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Translator.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Tracker.BlockNextUpdate();
        await fixture.Tracker.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var dispatcher = Application.Current.Dispatcher;
        var responsive = Task.Run(async () =>
        {
            try
            {
                await fixture.Translator.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await Task.Delay(100);
                var ping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = dispatcher.InvokeAsync(() => ping.TrySetResult());
                return await Task.WhenAny(ping.Task, Task.Delay(1000)) == ping.Task;
            }
            finally
            {
                fixture.Tracker.UpdateRelease.Set();
            }
        });
        fixture.Translator.Gate.Release.TrySetResult();
        Assert.True(await responsive.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.False(await fixture.Translator.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        await fixture.Presentation.Shown.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task VerifyHiddenDuringCacheAsync()
    {
        await using var fixture = new Fixture(delayCache: true);
        var hiddenTranslation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var translations = fixture.Translations.Subscribe(request =>
        {
            if (request is not null && !fixture.Model.OverlayVisible) hiddenTranslation.TrySetResult();
        });
        fixture.SubmitCapture(Bitmap(320));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "old"));
        await fixture.Cache.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Model.OverlayVisible = false;
        fixture.Cache.Release.Set();
        await Task.Delay(250);
        Assert.False(hiddenTranslation.Task.IsCompleted);

        fixture.Model.OverlayVisible = true;
        fixture.SubmitCapture(Bitmap(321));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
    }

    private static async Task VerifyOldHandoffAfterReopenAsync()
    {
        await using var fixture = new Fixture();
        fixture.Ocr.Text = "handoff-old";
        fixture.SubmitCapture(Bitmap(320));
        await WaitUntilAsync(() => fixture.Capture.StopCount > 0
            || fixture.Model.OcrTexts.Any(text => text.SourceText == "handoff-old"));
        var bitmap = fixture.Color.Bitmap!;
        if (fixture.Capture.StopCount == 0)
        {
            fixture.Model.OverlayVisible = false;
            fixture.Model.OverlayVisible = true;
        }
        await WaitUntilAsync(() => IsDisposed(bitmap));
        fixture.Ocr.Text = "new";
        fixture.SubmitCapture(Bitmap(321));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
        Assert.DoesNotContain(fixture.Model.OcrTexts, text => text.SourceText == "handoff-old");
    }

    private static async Task VerifyHiddenBeforeTranslationAsync()
    {
        await using var fixture = new Fixture(delayFilter: true);
        fixture.SubmitCapture(Bitmap(320));
        await fixture.Filter.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pending = Bitmap(322);
        fixture.Translations.OnNext(([new("pending", 0, 0, 100, 30, 20, false)],
            new() { SoftwareBitmap = pending, ImageSize = new(322, 240) }, fixture.Visibility));
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

    private static async Task VerifyDelayedCaptureAsync(bool isOneShotMode)
    {
        await using var fixture = new Fixture(isOneShotMode: isOneShotMode);
        var handler = fixture.Capture.CurrentHandler!;
        fixture.Model.OverlayVisible = false;
        Assert.Null(fixture.Capture.CurrentHandler);
        fixture.Model.OverlayVisible = true;
        Assert.NotNull(fixture.Capture.CurrentHandler);
        Assert.NotSame(handler, fixture.Capture.CurrentHandler);
        var stops = fixture.Capture.StopCount;
        var error = await Record.ExceptionAsync(() => handler.InvokeAsync(fixture.Capture, null!));
        await Task.Delay(150);
        Assert.Equal(stops, fixture.Capture.StopCount);
        Assert.Null(error);
        Assert.False(fixture.Ocr.Gate.Started.Task.IsCompleted);
    }

    private static async Task VerifyQueuedOneShotStopAsync()
    {
        await using var fixture = new Fixture(isOneShotMode: true);
        var handler = fixture.Capture.CurrentHandler!;
        using var submitted = new ManualResetEventSlim();
        var delayedCapture = Task.Run(() =>
        {
            var operation = handler.InvokeAsync(fixture.Capture, null!);
            submitted.Set();
            return operation;
        });
        Assert.True(submitted.Wait(TimeSpan.FromSeconds(5)));
        fixture.Model.OverlayVisible = false;
        fixture.Model.OverlayVisible = true;
        var stops = fixture.Capture.StopCount;
        await delayedCapture.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(stops, fixture.Capture.StopCount);
    }

    private static async Task VerifyOneShotAsync()
    {
        await using var fixture = new Fixture(Stage.Translation, captureInterval: 10, isOneShotMode: true);
        fixture.Model.OverlayVisible = false;
        fixture.Model.OverlayVisible = true;
        fixture.SubmitCapture(Bitmap(321));
        await fixture.Translator.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));

        try
        {
            fixture.Model.OverlayVisible = false;
            fixture.Model.OverlayVisible = true;
            fixture.SubmitCapture(Bitmap(320));
            await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "old"));
            Assert.DoesNotContain(fixture.Model.OcrTexts, text => text.SourceText == "new");
            Assert.Equal(0, fixture.Tracker.UpdateCount);
        }
        finally
        {
            fixture.Translator.Gate.Release.TrySetResult();
        }
    }

    private static async Task VerifyAutomaticWindowVisibilityAsync()
    {
        await using var fixture = new Fixture();
        fixture.Model.OverlayVisible = false;
        var target = new Window();
        var handle = new WindowInteropHelper(target).EnsureHandle();
        var desktop = new TestDesktopManager();
        var window = CreateOverlayWindow(null, handle, desktop, isOneShotMode: false);
        try
        {
            Assert.False(fixture.Model.OverlayVisible);
            Assert.Null(fixture.Capture.CurrentHandler);
            window.Show();
            StopPositionTimer(window);
            var overlay = (OverlayTextsControl)window.FindName("overlay");
            Assert.False(fixture.Model.OverlayVisible);
            window.DataContext = fixture.Model;
            Assert.True(fixture.Model.OverlayVisible);

            fixture.SubmitCapture(Bitmap(320));
            await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "old")
                && fixture.Translator.Gate.Count > 0);

            desktop.IsCurrent = false;
            typeof(OverlayMainWindow).GetMethod("UpdateWindowPositionAndSize", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, null);
            Assert.False(window.IsVisible);
            Assert.Equal(Visibility.Visible, overlay.Visibility);
            Assert.False(fixture.Model.OverlayVisible);
            Assert.Null(fixture.Capture.CurrentHandler);

            overlay.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Hidden);
            overlay.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            Assert.False(fixture.Model.OverlayVisible);
            Assert.Null(fixture.Capture.CurrentHandler);
            var trackerCount = fixture.Tracker.UpdateCount;
            var colorCount = fixture.Color.Gate.Count;
            var translationCount = fixture.Translator.Gate.Count;
            await Task.Delay(350);
            Assert.Equal(trackerCount, fixture.Tracker.UpdateCount);
            Assert.Equal(colorCount, fixture.Color.Gate.Count);
            Assert.Equal(translationCount, fixture.Translator.Gate.Count);

            window.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            Assert.True(fixture.Model.OverlayVisible);
            await Dispatcher.Yield(DispatcherPriority.Background);
            Assert.Empty(fixture.Model.OcrTexts);
            fixture.Ocr.Text = "new";
            fixture.SubmitCapture(Bitmap(321));
            await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
            Assert.DoesNotContain(fixture.Model.OcrTexts, text => text.SourceText == "old");

            desktop.IsCurrent = true;
            typeof(OverlayMainWindow).GetMethod("UpdateWindowPositionAndSize", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, null);
            Assert.False(window.IsVisible);
            Assert.False(fixture.Model.OverlayVisible);
            window.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);

            overlay.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Hidden);
            Assert.False(fixture.Model.OverlayVisible);
            window.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Hidden);
            window.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            Assert.False(fixture.Model.OverlayVisible);
            overlay.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            Assert.True(fixture.Model.OverlayVisible);

            var stopsAtDispose = -1;
            window.Closed += (_, _) =>
            {
                fixture.Model.Dispose();
                stopsAtDispose = fixture.Capture.StopCount;
            };
            window.Close();
            Assert.Equal(stopsAtDispose, fixture.Capture.StopCount);
        }
        finally
        {
            window.Close();
            target.Close();
        }
    }

    private static async Task VerifyOneShotWindowVisibilityAsync()
    {
        await using var fixture = new Fixture(isOneShotMode: true);
        var target = new Window();
        var handle = new WindowInteropHelper(target).EnsureHandle();
        var window = CreateOverlayWindow(fixture.Model, handle, new TestDesktopManager(), isOneShotMode: true);
        try
        {
            Assert.False(fixture.Model.OverlayVisible);
            window.Show();
            StopPositionTimer(window);
            var overlay = (OverlayTextsControl)window.FindName("overlay");
            Assert.False(fixture.Model.OverlayVisible);
            Assert.Null(fixture.Capture.CurrentHandler);

            overlay.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            Assert.True(fixture.Model.OverlayVisible);
            Assert.NotNull(fixture.Capture.CurrentHandler);
            fixture.SubmitCapture(Bitmap(321));
            await WaitUntilAsync(() => fixture.Model.OcrTexts.Any(text => text.SourceText == "new"));
            overlay.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Hidden);
            Assert.False(fixture.Model.OverlayVisible);
        }
        finally
        {
            window.Close();
            target.Close();
        }
    }

    private static OverlayMainWindow CreateOverlayWindow(
        OverlayMainViewModel? model, IntPtr target, IVirtualDesktopManager desktop, bool isOneShotMode)
        => new(new Snapshot<CommonSettings>(new()),
            new Snapshot<TargetSettings>(new() { IsOneShotMode = isOneShotMode }),
            new ProcessInfo(target), desktop, NullLogger<OverlayMainWindow>.Instance)
        {
            ShowActivated = false,
            Left = -10000,
            Top = -10000,
            Width = 1,
            Height = 1,
            DataContext = model,
        };

    private static void StopPositionTimer(OverlayMainWindow window)
        => ((DispatcherTimer)typeof(OverlayMainWindow).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window)!).Stop();

    private static async Task VerifyDisposeAsync()
    {
        await using var fixture = new Fixture(Stage.Ocr);
        var image = Bitmap(320);
        fixture.SubmitCapture(image);
        await fixture.Ocr.Gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Run(fixture.Model.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Ocr.Gate.Release.TrySetResult();
        await WaitUntilAsync(() => IsDisposed(image));
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

        public Fixture(Stage? delayed = null, double captureInterval = 0, bool delayReset = false, bool delayFilter = false, bool withoutContext = false, bool failFirst = false, bool delayUpdate = false, bool isOneShotMode = false, bool emptyFirstCapture = false, bool delayNextFilter = false, bool delayCache = false)
        {
            this.Ocr = new(delayed == Stage.Ocr, failFirst && delayed == Stage.Ocr, emptyFirstCapture);
            this.Color = new(delayed == Stage.Color);
            this.Translator = new(delayed == Stage.Translation, failFirst && delayed == Stage.Translation);
            this.Tracker = new(delayReset, delayUpdate);
            this.Filter = new(delayFilter, delayNextFilter);
            this.Cache = new(delayCache);
            var presentation = DispatchProxy.Create<IPresentationService, PresentationProxy>();
            this.Presentation = (PresentationProxy)presentation;
            var context = SynchronizationContext.Current;
            try
            {
                if (withoutContext) SynchronizationContext.SetSynchronizationContext(null);
                this.Model = new OverlayMainViewModel(
                    presentation, new Snapshot<TargetSettings>(new() { CaptureInterval = captureInterval, IsOneShotMode = isOneShotMode }), new ProcessInfo(),
                    this.Capture, this.Ocr, new Snapshot<BasicOcrParam>(new()), this.Tracker, this.recorder,
                    this.Translator, this.Cache, this.Color, delayFilter ? [this.Filter] : [], NullLogger<OverlayMainViewModel>.Instance);
            }
            finally
            {
                if (withoutContext) SynchronizationContext.SetSynchronizationContext(context);
            }
        }

        public Capture Capture { get; } = new();
        public Ocr Ocr { get; }
        public Tracker Tracker { get; }
        public Color Color { get; }
        public Translator Translator { get; }
        public Cache Cache { get; }
        public Filter Filter { get; }
        public PresentationProxy Presentation { get; }
        public OverlayMainViewModel Model { get; }
        public Subject<(IEnumerable<TextRect>? Texts, FilterContext? Context, Observer<Unit>? Visibility)> Tracking
            => Field<Subject<(IEnumerable<TextRect>?, FilterContext?, Observer<Unit>?)>>("trackingRequests");
        public Subject<(IEnumerable<TextRect> Texts, FilterContext Context, Observer<Unit> Visibility)?> Translations
            => Field<Subject<(IEnumerable<TextRect>, FilterContext, Observer<Unit>)?>>("translationRequests");
        public Observer<Unit> Visibility => (Observer<Unit>)Field<SerialDisposable>("captureLoop").Disposable!;

        public void SubmitCapture(SoftwareBitmap bitmap)
            => Field<Subject<(SoftwareBitmap, Observer<Unit>)>>("captureRequests").OnNext((bitmap, this.Visibility));

        private T Field<T>(string name) where T : class
            => (T)typeof(MainViewModelBase).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this.Model)!;

        public async ValueTask DisposeAsync()
        {
            this.Model.Dispose();
            await this.recorder.DisposeAsync();
            this.Tracker.ResetRelease.Dispose();
            this.Tracker.UpdateRelease.Dispose();
            this.Cache.Release.Dispose();
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

    private sealed class ProcessInfo(IntPtr handle = default) : IProcessInfoStore
    {
        public IntPtr MainWindowHandle => handle;
        public string Name => "test";
    }

    private sealed class TestDesktopManager : IVirtualDesktopManager
    {
        public bool IsCurrent { get; set; } = true;
        public bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow) => this.IsCurrent;
        public Guid GetWindowDesktopId(IntPtr hWnd) => Guid.Empty;
        public int MoveWindowToDesktop(IntPtr hWnd, ref Guid desktop) => 0;
    }

    private sealed class Capture : ICaptureModule
    {
        private int stops;
        public event AsyncEventHandler<CapturedEventArgs>? Captured;
        public AsyncEventHandler<CapturedEventArgs>? CurrentHandler => this.Captured;
        public int StopCount => Volatile.Read(ref this.stops);
        public void StartCapture(IntPtr targetWindow) { }
        public void StopCapture() => Interlocked.Increment(ref this.stops);
    }

    private sealed class Ocr(bool delayed, bool failFirst, bool emptyFirstCapture) : IOcrModule
    {
        public Gate Gate { get; } = new(delayed);
        public string? Text { get; set; }
        public async ValueTask<IReadOnlyList<TextRect>> RecognizeAsync(OcrCaptureInput input)
        {
            await this.Gate.EnterAsync();
            if (failFirst && this.Gate.Count == 1) throw new InvalidOperationException("模擬 OCR エラー");
            if (emptyFirstCapture && input.Source.PixelWidth == 320) return [];
            var text = this.Text ?? (input.Source.PixelWidth == 321 ? "new" : "old");
            return [new TextRect(text, 0, 0, 100, 30, 20, false)];
        }
    }

    private sealed class Tracker(bool delayReset, bool delayUpdate) : IOcrTextTracker
    {
        private int updates;
        private int resets;
        private int blockNext;
        private TextRect[] previous = [];
        public TaskCompletionSource ResetStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ResetRelease { get; } = new();
        public ManualResetEventSlim UpdateRelease { get; } = new();
        public TaskCompletionSource Updated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int UpdateCount => Volatile.Read(ref this.updates);
        public int ResetCount => Volatile.Read(ref this.resets);
        public void BlockNextUpdate() => Interlocked.Exchange(ref this.blockNext, 1);
        public IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, System.Drawing.Size imageSize, TimeSpan? timestamp = null)
        {
            var count = Interlocked.Increment(ref this.updates);
            this.Updated.TrySetResult();
            if ((delayUpdate && count == 1) || Interlocked.Exchange(ref this.blockNext, 0) == 1)
            {
                this.Blocked.TrySetResult();
                if (!this.UpdateRelease.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
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
        public TaskCompletionSource<bool> Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        {
            await this.Gate.EnterAsync();
            if (failFirst && this.Gate.Count == 1)
            {
                this.Failed.TrySetResult(Application.Current.Dispatcher.CheckAccess());
                throw new InvalidOperationException("模擬翻訳エラー");
            }
            return srcTexts.Select(text => text.SourceText).ToArray();
        }
    }

    public class PresentationProxy : DispatchProxy
    {
        public TaskCompletionSource Shown { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IPresentationService.OpenErrorReportDialogAsync))
            {
                this.Shown.TrySetResult();
                return Task.FromResult(true);
            }
            throw new NotSupportedException(method?.Name);
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

    private sealed class Cache(bool delayed) : ICacheModule
    {
        private int containsCount;
        public int ContainsCount => Volatile.Read(ref this.containsCount);
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public bool Contains(string src)
        {
            var count = Interlocked.Increment(ref this.containsCount);
            if (delayed && count == 2)
            {
                this.Started.TrySetResult();
                if (!this.Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            }
            return false;
        }
        public string Get(string src) => throw new InvalidOperationException();
        public void AddRange(IEnumerable<(string src, string dst)> pairs) { }
    }
}
