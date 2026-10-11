using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.PluginStore;
using WindowTranslator.Modules.Startup;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using ProgressBar = System.Windows.Controls.ProgressBar;

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
        var entryAssembly = Assembly.GetEntryAssembly();
        try
        {
            Assert.Null(Application.Current);
            // testhostではなく本体のアセンブリから、既存XAMLの相対リソースを解決する。
            Assembly.SetEntryAssembly(typeof(App).Assembly);
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
                        Assert.True(titleBar.ShowClose);
                        Assert.NotNull(titleBar.Template);
                        Assert.True(titleBar.ActualHeight > 0);
                        Assert.False(titleBar.ShowMinimize);
                        Assert.Same(setup.Resources, UiApplication.Current.Resources);
                        var lightForeground = GetForegroundColor(setup, ApplicationTheme.Light);
                        var darkForeground = GetForegroundColor(setup, ApplicationTheme.Dark);
                        Assert.NotEqual(lightForeground, darkForeground);
                        ApplicationThemeManager.ApplySystemTheme();

                        var buttons = Descendants<Wpf.Ui.Controls.Button>(setup).ToArray();
                        var install = Assert.Single(buttons, button => button.Command == viewModel.InstallCommand);
                        var skip = Assert.Single(buttons, button => button.Command == viewModel.FinishCommand);
                        Assert.False(install.IsEnabled);
                        Assert.True(skip.IsEnabled);
                        Assert.InRange(skip.ActualHeight, 24, 60);
                        Assert.True(skip.TransformToAncestor(setup).Transform(new Point()).Y + skip.ActualHeight <= setup.ActualHeight);
                        var setupPackage = new PluginSetupPackage(new(
                            "WindowTranslator.Plugin.OneOcrPlugin", "OneOCR Plugin", "WindowsのOCRエンジンを使用します。",
                            "Freesia", null, null, ["1.0.0"], IsOfficial: true), configuration);
                        setupPackage.Package.ReadmeMarkdown = "# README content";
                        viewModel.Groups = [new("OcrModule", [setupPackage])];
                        viewModel.IsLoading = false;
                        setup.UpdateLayout();
                        Assert.True(install.IsEnabled);
                        Assert.DoesNotContain(Descendants<TextBlock>(setup), text =>
                            text.Text == "WindowsのOCRエンジンを使用します。");
                        Assert.Contains(Descendants<System.Windows.Controls.CheckBox>(setup), checkbox =>
                            Equals(System.Windows.Automation.AutomationProperties.GetName(checkbox), "OneOCR Plugin"));
                        var expander = Assert.Single(Descendants<System.Windows.Controls.Expander>(setup));
                        Assert.Equal("OneOCR Plugin", expander.Header);
                        var packageRow = Assert.Single(Descendants<System.Windows.Controls.StackPanel>(setup), panel =>
                            panel.Children.OfType<System.Windows.Controls.Grid>().Any(grid =>
                                grid.Children.Contains(expander)));
                        Assert.InRange(packageRow.ActualHeight, 24, 65);
                        Assert.Contains(packageRow.Children.OfType<System.Windows.Controls.TextBlock>(), text =>
                            text.Visibility == Visibility.Collapsed);
                        var help = Assert.Single(Descendants<HyperlinkButton>(setup));
                        Assert.Equal(
                            HelpUriBuilder.Build("OcrModule", System.Globalization.CultureInfo.CurrentUICulture),
                            help.NavigateUri);
                        var headingPanel = Assert.Single(Descendants<System.Windows.Controls.StackPanel>(setup), panel =>
                            panel.Orientation == System.Windows.Controls.Orientation.Horizontal
                            && panel.Children.OfType<System.Windows.Controls.ItemsControl>().Any(items =>
                                ReferenceEquals(items.ItemsSource, viewModel.Groups[0].HelpLinks)));
                        var heading = Assert.Single(headingPanel.Children.OfType<System.Windows.Controls.TextBlock>());
                        Assert.Equal("OCR", heading.Text);
                        var progress = Assert.Single(Descendants<ProgressBar>(setup));
                        var progressContainer = Assert.IsType<System.Windows.Controls.StackPanel>(
                            VisualTreeHelper.GetParent(progress));
                        var buttonContainer = Assert.IsType<System.Windows.Controls.StackPanel>(
                            VisualTreeHelper.GetParent(skip));
                        Assert.Equal(2, System.Windows.Controls.Grid.GetRow(progressContainer));
                        Assert.Equal(3, System.Windows.Controls.Grid.GetRow(buttonContainer));
                        expander.IsExpanded = true;
                        setup.UpdateLayout();
                        var details = Assert.IsType<System.Windows.Controls.StackPanel>(expander.Content);
                        Assert.DoesNotContain(details.Children.OfType<System.Windows.Controls.TextBlock>(), text =>
                            text.Text == "WindowsのOCRエンジンを使用します。");
                        var markdown = Assert.Single(details.Children.OfType<MdXaml.MarkdownScrollViewer>());
                        Assert.Equal("# README content", markdown.Markdown);
                        Assert.Equal(Visibility.Visible, markdown.Visibility);
                        Assert.Same(setup.FindResource("mdStyle"), markdown.MarkdownStyle);
                        Assert.NotNull(markdown.Document);
                        var headingParagraph = Assert.Single(markdown.Document!.Blocks.OfType<Paragraph>(),
                            paragraph => Equals(paragraph.Tag, "Heading1"));
                        var expectedHeadingColor = Assert.IsType<SolidColorBrush>(
                            setup.FindResource("TextFillColorSecondaryBrush")).Color;
                        Assert.Equal(expectedHeadingColor,
                            Assert.IsType<SolidColorBrush>(headingParagraph.Foreground).Color);
                        setup.Close();
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        if (!viewModel.IsCompleted)
                        {
                            await viewModel.FinishCommand.ExecuteAsync(null);
                        }
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
                        PluginSetup.AttachApplicationTheme();
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
            Assembly.SetEntryAssembly(entryAssembly);
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

    private static Color GetForegroundColor(FrameworkElement element, ApplicationTheme theme)
    {
        ApplicationThemeManager.Apply(theme);
        return Assert.IsType<SolidColorBrush>(element.FindResource("TextFillColorPrimaryBrush")).Color;
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
