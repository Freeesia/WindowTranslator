using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WindowTranslator.Modules.PluginStore;

namespace WindowTranslator.Modules.Startup;

internal static class StartupWindowFactory
{
    public static Window Create(IServiceProvider services)
    {
        if (!services.GetRequiredService<NuGetPluginService>().IsSetupRequired)
        {
            return CreateStartupDialog(services);
        }

        var viewModel = services.GetRequiredService<PluginSetupViewModel>();
        var window = new PluginSetupWindow(viewModel);
        viewModel.Completed += (_, _) =>
        {
            if (viewModel.RequiresRestart)
            {
                ApplicationRestart.Restart();
                return;
            }

            var app = services.GetRequiredService<App>();
            var startupDialog = CreateStartupDialog(services);
            // OnMainWindowCloseによる終了を避けるため、閉じる前にメインウィンドウを引き継ぐ。
            app.MainWindow = startupDialog;
            startupDialog.Show();
            app.CompleteStartup();
            window.Close();
        };
        return window;
    }

    private static StartupDialog CreateStartupDialog(IServiceProvider services)
    {
        var window = services.GetRequiredService<StartupDialog>();
        window.DataContext = services.GetRequiredService<StartupViewModel>();
        return window;
    }
}
