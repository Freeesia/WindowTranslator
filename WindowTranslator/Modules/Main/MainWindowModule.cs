using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Windows;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Main;
public class MainWindowModule : IMainWindowModule
{
    private readonly App app;
    private readonly IServiceProvider provider;

    public MainWindowModule(App app, IServiceProvider provider)
    {
        this.app = app;
        this.provider = provider;
    }

    public async Task OpenTargetAsync(IntPtr mainWindowHandle, string name)
    {
        var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<UserSettings>>();
        var presentationService = scope.ServiceProvider.GetRequiredService<IPresentationService>();
        var processInfo = scope.ServiceProvider.GetRequiredService<IProcessInfoStore>();
        processInfo.SetTargetProcess(mainWindowHandle, name);
        switch (options.Value.ViewMode)
        {
            case ViewMode.Capture:
                await presentationService.OpenCaptureMainWindowAsync();
                break;
            case ViewMode.Overlay:
                await presentationService.OpenOverlayMainWindowAsync();
                break;
            default:
                throw new NotSupportedException();
        }
        var window = this.app.Windows.OfType<Window>().Single(w => w.IsActive);
        window.Tag = scope;
        window.Closed += Window_Closed;
    }

    private static void Window_Closed(object? sender, EventArgs e)
    {
        var window = (Window)sender!;
        window.Closed -= Window_Closed;
        var scope = (IServiceScope)window.Tag;
        scope.Dispose();
    }
}

public interface IMainWindowModule
{
    Task OpenTargetAsync(IntPtr mainWindowHandle, string name);
}
