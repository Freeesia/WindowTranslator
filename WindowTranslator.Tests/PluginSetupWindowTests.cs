using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.PluginStore;
using WindowTranslator.Modules.Startup;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WindowTranslator.Tests;

[CollectionDefinition("WPF startup", DisableParallelization = true)]
public sealed class WpfStartupCollection;

[Collection("WPF startup")]
public sealed class PluginSetupWindowTests
{
    [Fact]
    public async Task SetupCompletesBeforeApplicationAndNormalHostStartsOnTheSameThread()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                VerifyStartup();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    private static void VerifyStartup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WindowTranslator.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.Null(Application.Current);
            Assert.True(PluginSetup.IsRequired(directory));
            using var service = new NuGetPluginService(
                NullLogger<NuGetPluginService>.Instance, new UnusedHttpClientFactory(),
                Repository.Factory.GetCoreV3("https://nuget.test/v3/index.json"), directory,
                NuGetPluginService.CreateHostPackageVersions(), AppInfo.Instance.Version.Major);
            using var configuration = new ConfigurationManager();
            using var viewModel = new PluginSetupViewModel(service, configuration,
                NullLogger<PluginSetupViewModel>.Instance, Dispatcher.CurrentDispatcher);
            var setup = new PluginSetupWindow(viewModel);
            Exception? failure = null;
            setup.Loaded += (_, _) =>
            {
                _ = setup.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        Assert.Null(Application.Current);
                        Assert.Empty(Descendants<ContentDialog>(setup));
                        var titleBar = Assert.Single(Descendants<TitleBar>(setup));
                        Assert.False(titleBar.ShowClose);
                        Assert.NotNull(titleBar.Template);
                        Assert.True(titleBar.ActualHeight > 0);
                        foreach (var theme in new[] { ApplicationTheme.Light, ApplicationTheme.Dark })
                        {
                            ApplicationThemeManager.Apply(theme);
                            Assert.Equal(
                                Assert.IsType<SolidColorBrush>(UiApplication.Current.TryFindResource("TextFillColorPrimaryBrush")).Color,
                                Assert.IsType<SolidColorBrush>(setup.FindResource("TextFillColorPrimaryBrush")).Color);
                        }
                        setup.Close();
                        Assert.True(setup.IsVisible);

                        var buttons = Descendants<Wpf.Ui.Controls.Button>(setup).ToArray();
                        var install = Assert.Single(buttons, button => button.Command == viewModel.InstallCommand);
                        var skip = Assert.Single(buttons, button => button.Command == viewModel.FinishCommand);
                        Assert.False(install.IsEnabled);
                        Assert.True(skip.IsEnabled);
                        Assert.InRange(skip.ActualHeight, 24, 60);
                        Assert.True(skip.TransformToAncestor(setup).Transform(new Point()).Y + skip.ActualHeight <= setup.ActualHeight);
                        viewModel.Groups = [new("OCR", [new PluginSetupPackage(new(
                            "WindowTranslator.Plugin.OneOcrPlugin", "OneOCR", "WindowsのOCRエンジンを使用します。",
                            "Freesia", null, null, ["1.0.0"], IsOfficial: true), configuration)])];
                        viewModel.IsLoading = false;
                        setup.UpdateLayout();
                        Assert.True(install.IsEnabled);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        await viewModel.FinishCommand.ExecuteAsync(null);
                    }
                }, DispatcherPriority.ApplicationIdle);
            };
            setup.ShowDialog();
            if (failure is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            Assert.True(viewModel.IsCompleted);
            Assert.False(setup.IsVisible);
            Assert.Null(Application.Current);
            Assert.False(PluginSetup.IsRequired(directory));
            Assert.False(service.IsRestartRequired);

            // 前段のShowDialogと同じSTAで、従来どおりの本体ホストを初めて起動する。
            var builder = KamishibaiApplication<App, StartupDialog>.CreateBuilder();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Services.AddSingleton<IMainWindowModule, MainWindowModule>();
            builder.Services.AddSingleton<IVirtualDesktopManager, TestDesktopManager>();
            builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
            var host = builder.Build();
            var verified = false;
            host.Loaded += (_, e) =>
            {
                _ = e.Window.Dispatcher.InvokeAsync(async () =>
                {
                    var app = Assert.IsType<App>(Application.Current);
                    try
                    {
                        PluginSetup.AttachApplicationTheme(e.Window);
                        Assert.Same(app.Resources, UiApplication.Current.Resources);
                        Assert.Same(e.Window, app.MainWindow);
                        Assert.Single(app.Windows.Cast<Window>());
                        Assert.IsType<StartupViewModel>(e.Window.DataContext);
                        Assert.True(app.WaitForStartupAsync().IsCompletedSuccessfully);
                        Assert.True(e.Window.IsVisible);
                        // 閉じたセットアップに残るWPF-UIのイベントが、通常起動を壊さないこと。
                        ApplicationThemeManager.Apply(ApplicationTheme.Light);
                        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

                        // 自動起動時に通常画面を隠す既存の処理も変更しない。
                        builder.Configuration[nameof(LaunchMode)] = nameof(LaunchMode.Startup);
                        var hidden = host.Services.GetRequiredService<StartupDialog>();
                        hidden.Show();
                        await hidden.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                        Assert.False(hidden.IsVisible);
                        verified = true;
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        app.Shutdown();
                    }
                }, DispatcherPriority.ApplicationIdle);
            };
#pragma warning disable VSTHRD002 // 専用STAでApplication.Runのメッセージループを実行する。
            host.RunAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            if (failure is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            Assert.True(verified);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private sealed class UnusedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("このUIテストでは通信しません。");
    }

    private sealed class TestDesktopManager : IVirtualDesktopManager
    {
        public bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow) => true;
        public Guid GetWindowDesktopId(IntPtr hWnd) => Guid.Empty;
        public int MoveWindowToDesktop(IntPtr hWnd, ref Guid desktop) => 0;
    }
}
