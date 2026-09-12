using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace WindowTranslator.Modules.PluginStore;

internal static class PluginSetup
{
    public static bool ShowIfRequired(string[] args)
    {
        var directory = Path.Combine(PathUtility.UserDir, "nuget-plugins");
        if (!IsRequired(directory))
        {
            return false;
        }

        // 本体と同じSTAを使い、WPF-UIの静的テーマイベントを別スレッドに持ち越さない。
        if (!Thread.CurrentThread.TrySetApartmentState(ApartmentState.STA))
        {
            Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
            Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
        }
        using var configuration = new ConfigurationManager();
        configuration.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .AddJsonFile(PathUtility.UserSettings, optional: true);
        using var services = new ServiceCollection()
            .AddHttpClient(NuGetPluginService.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
            .Services.BuildServiceProvider();
        using var service = new NuGetPluginService(
            NullLogger<NuGetPluginService>.Instance, services.GetRequiredService<IHttpClientFactory>(),
            Repository.Factory.GetCoreV3(NuGetPluginService.NuGetServiceIndexUrl), directory,
            NuGetPluginService.CreateHostPackageVersions(), AppInfo.Instance.Version.Major);
        Show(service, configuration);
        return true;
    }

    internal static bool IsRequired(string directory)
        => !File.Exists(Path.Combine(directory, "nuget-manifest.json"));

    internal static void Show(NuGetPluginService service, IConfiguration configuration)
    {
        using var viewModel = new PluginSetupViewModel(service, configuration,
            NullLogger<PluginSetupViewModel>.Instance, Dispatcher.CurrentDispatcher);
        var window = new PluginSetupWindow(viewModel);
        // Application.Runは呼ばず、ShowDialogのメッセージループだけでセットアップを完了する。
#pragma warning disable VSTHRD002
        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
        try
        {
            window.ShowDialog();
        }
        finally
        {
            service.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
#pragma warning restore VSTHRD002
    }

    internal static void AttachApplicationTheme(Window window)
    {
        // Applicationなしで作られたWPF-UIのリソース参照を、本体のApplicationへ接続する。
        UiApplication.Current.Resources = Application.Current.Resources;
        UiApplication.Current.MainWindow = window;
        ApplicationThemeManager.ApplySystemTheme();
    }
}
