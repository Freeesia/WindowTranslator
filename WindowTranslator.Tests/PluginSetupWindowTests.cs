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
using Wpf.Ui.Controls;

namespace WindowTranslator.Tests;

[CollectionDefinition("WPF startup", DisableParallelization = true)]
public sealed class WpfStartupCollection;

[Collection("WPF startup")]
public sealed class PluginSetupWindowTests
{
    [Fact]
    public async Task SetupIsTheFirstWindowAndSkipTransfersMainWindowWithoutShutdown()
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
            // 実際のAppとホストを使いつつ、設定とインストール先を利用者の環境から分離する。
            var builder = KamishibaiApplication<App, Window>.CreateBuilder();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Services.AddSingleton<IMainWindowModule, MainWindowModule>();
            builder.Services.AddSingleton<IVirtualDesktopManager, TestDesktopManager>();
            builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
            builder.Services.AddTransient<PluginSetupViewModel>();
            builder.Services.AddSingleton(_ => new NuGetPluginService(
                NullLogger<NuGetPluginService>.Instance,
                new UnusedHttpClientFactory(),
                Repository.Factory.GetCoreV3("https://nuget.test/v3/index.json"),
                directory, NuGetPluginService.CreateHostPackageVersions(), AppInfo.Instance.Version.Major));
            IServiceProvider? services = null;
            builder.Host.ConfigureContainer<IServiceCollection>((_, collection) => collection.AddTransient<Window>(provider =>
            {
                services = provider;
                return StartupWindowFactory.Create(provider);
            }));
            var host = builder.Build();
            Exception? failure = null;
            var verified = false;
            host.Loaded += (_, e) =>
            {
                _ = e.Window.Dispatcher.InvokeAsync(async () =>
                {
                    var app = (App)Application.Current;
                    try
                    {
                        var window = Assert.IsType<PluginSetupWindow>(e.Window);
                        Assert.Same(window, app.MainWindow);
                        Assert.Single(app.Windows.Cast<Window>());
                        Assert.True(app.WaitForStartupAsync().IsCompletedSuccessfully);
                        Assert.IsAssignableFrom<FluentWindow>(window);
                        Assert.Empty(Descendants<ContentDialog>(window));
                        var titleBar = Assert.Single(Descendants<TitleBar>(window));
                        Assert.False(titleBar.ShowClose);
                        Assert.True(titleBar.ActualHeight > 0);
                        Assert.NotNull(titleBar.Template);
                        window.Close();
                        Assert.True(window.IsVisible);

                        var viewModel = Assert.IsType<PluginSetupViewModel>(window.DataContext);
                        var buttons = Descendants<Wpf.Ui.Controls.Button>(window).ToArray();
                        var install = Assert.Single(buttons, button => button.Command == viewModel.InstallCommand);
                        var skip = Assert.Single(buttons, button => button.Command == viewModel.FinishCommand);
                        Assert.False(install.IsEnabled);
                        Assert.True(skip.IsEnabled);
                        Assert.InRange(skip.ActualHeight, 24, 60);
                        Assert.True(skip.TransformToAncestor(window).Transform(new Point()).Y + skip.ActualHeight <= window.ActualHeight);

                        viewModel.Groups = [new("OCR", [new PluginSetupPackage(new(
                            "WindowTranslator.Plugin.OneOcrPlugin", "OneOCR", "WindowsのOCRエンジンを使用します。",
                            "Freesia", null, null, ["1.0.0"], IsOfficial: true), builder.Configuration)])];
                        viewModel.IsLoading = false;
                        window.UpdateLayout();
                        Assert.True(install.IsEnabled);

                        await viewModel.FinishCommand.ExecuteAsync(null);

                        Assert.True(viewModel.IsCompleted);
                        Assert.False(viewModel.RequiresRestart);
                        Assert.False(window.IsVisible);
                        var startup = Assert.IsType<StartupDialog>(app.MainWindow);
                        Assert.IsType<StartupViewModel>(startup.DataContext);
                        Assert.True(startup.IsVisible);
                        Assert.True(app.WaitForStartupAsync().IsCompletedSuccessfully);
                        Assert.Single(app.Windows.Cast<Window>());
                        Assert.True(File.Exists(Path.Combine(directory, "nuget-manifest.json")));

                        // 次回起動相当ではセットアップを作らず、自動起動時の非表示も維持する。
                        var provider = Assert.IsAssignableFrom<IServiceProvider>(services);
                        provider.GetRequiredService<IConfiguration>()[nameof(LaunchMode)] = nameof(LaunchMode.Startup);
                        var existing = Assert.IsType<StartupDialog>(StartupWindowFactory.Create(provider));
                        Assert.IsType<StartupViewModel>(existing.DataContext);
                        existing.Show();
                        await existing.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                        Assert.False(existing.IsVisible);
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
            // 専用STAスレッドでは、ホスト内部のApplication.Runがメッセージループを実行する。
#pragma warning disable VSTHRD002
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
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
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
