using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Main;
public class MainWindowModule(App app, IServiceProvider provider) : IMainWindowModule
{
    private readonly App app = app;
    private readonly IServiceProvider provider = provider;
    public ObservableCollection<WindowInfo> OpenedWindows { get; } = new();

    public Task OpenTargetAsync(IntPtr mainWindowHandle, string name)
        => this.app.Dispatcher.Invoke(() => OpenTargetWindowCoreAsync(mainWindowHandle, name));

    private async Task OpenTargetWindowCoreAsync(IntPtr mainWindowHandle, string name)
    {
        var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<UserSettings>>();
        var presentationService = scope.ServiceProvider.GetRequiredService<IPresentationService>();
        var processInfo = scope.ServiceProvider.GetRequiredService<IProcessInfoStoreInternal>();
        processInfo.SetTargetProcess(mainWindowHandle, name);
        var window = options.Value.ViewMode switch
        {
            ViewMode.Capture => await presentationService.OpenCaptureMainWindowAsync(),
            ViewMode.Overlay => await presentationService.OpenOverlayMainWindowAsync(),
            _ => throw new NotSupportedException(),
        };
        var info = new WindowInfo(name, window);
        window.Closed += (_, _) =>
        {
            scope.Dispose();
            this.OpenedWindows.Remove(info);
        };
        this.OpenedWindows.Add(info);
    }
}

public interface IMainWindowModule
{
    ObservableCollection<WindowInfo> OpenedWindows { get; }

    Task OpenTargetAsync(IntPtr mainWindowHandle, string name);
}

public record WindowInfo(string Name, IWindow Window);
