using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using System.Collections.ObjectModel;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Main;
public sealed class MainWindowModule(App app, IServiceProvider provider) : IMainWindowModule, IDisposable
{
    private readonly App app = app;
    private readonly IServiceProvider provider = provider;
    private readonly AsyncSemaphore asyncLock = new(1);

    public ObservableCollection<WindowInfo> OpenedWindows { get; } = new();

    public Task OpenTargetAsync(IntPtr targetWindowHandle, string name)
        => this.app.Dispatcher.Invoke(() => OpenTargetWindowCoreAsync(targetWindowHandle, name));

    private async Task OpenTargetWindowCoreAsync(IntPtr targetWindowHandle, string name)
    {
        using var l = await this.asyncLock.EnterAsync();
        var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<UserSettings>>();
        var presentationService = scope.ServiceProvider.GetRequiredService<IPresentationService>();
        var processInfo = scope.ServiceProvider.GetRequiredService<IProcessInfoStoreInternal>();
        processInfo.SetTargetProcess(targetWindowHandle, name);

        if (!options.Value.Targets.ContainsKey(name))
        {
            await presentationService.OpenAllSettingsDialogAsync(name);
        }

        var window = options.Value.Common.ViewMode switch
        {
            ViewMode.Capture => await presentationService.OpenCaptureMainWindowAsync(),
            ViewMode.Overlay => await presentationService.OpenOverlayMainWindowAsync(),
            _ => throw new NotSupportedException(),
        };
        var info = new WindowInfo(name, targetWindowHandle, window);
        window.Closed += (_, _) =>
        {
            scope.Dispose();
            this.OpenedWindows.Remove(info);
        };
        this.OpenedWindows.Add(info);
    }

    public void Dispose()
        => this.asyncLock.Dispose();
}

public interface IMainWindowModule
{
    ObservableCollection<WindowInfo> OpenedWindows { get; }

    Task OpenTargetAsync(IntPtr targetWindowHandle, string name);
}

public record WindowInfo(string Name, IntPtr Target, IWindow Window);
