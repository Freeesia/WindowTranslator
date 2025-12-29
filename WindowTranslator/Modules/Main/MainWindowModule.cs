using System.Collections.ObjectModel;
using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using WindowTranslator.Properties;
using WindowTranslator.Stores;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Modules.Main;

public sealed class MainWindowModule(App app, IServiceProvider provider, ILogger<MainWindowModule> logger) : IMainWindowModule, IDisposable
{
    private readonly App app = app;
    private readonly IServiceProvider provider = provider;
    private readonly ILogger<MainWindowModule> logger = logger;
    private readonly AsyncSemaphore asyncLock = new(1);

    public ObservableCollection<WindowInfo> OpenedWindows { get; } = new();

    public Task OpenTargetAsync(IntPtr targetWindowHandle, string name)
        => this.app.Dispatcher.Invoke(() => OpenTargetWindowCoreAsync(targetWindowHandle, name));

    private async ValueTask<TargetSettings?> GetSettingsAsync(string name)
    {
        using var scope = provider.CreateScope();
        var presentationService = scope.ServiceProvider.GetRequiredService<IPresentationService>();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<UserSettings>>();
        // 対象の設定を取得
        if (options.Value.Targets.TryGetValue(name, out var settings))
        {
            // 設定を検証
            var validationResults = await presentationService.OpenValidateAsync(settings);
            if (validationResults.IsEmpty())
            {
                this.logger.LogInformation($"Settings for target '{name}' are valid.");
                return settings;
            }

            // 検証エラーがある場合、エラーダイアログを表示
            var result = await presentationService.ShowMessageAsync(new(
                string.Format(Resources.InvalidSettings, name),
                Resources.InvalidSettingsContent + string.Join("\n\n", validationResults.Select(r => $"### {r.Title}\n{r.Message}")))
            {
                PrimaryButtonText = Resources.Settings,
                SecondaryButtonText = Resources.RunAsIs,
                CloseButtonText = Resources.Cancel,
            });

            switch (result)
            {
                case Wpf.Ui.Controls.MessageBoxResult.Primary:
                    this.logger.LogInformation($"User chose to open settings for target '{name}'.");
                    break;
                case Wpf.Ui.Controls.MessageBoxResult.Secondary:
                    this.logger.LogWarning($"Settings for target '{name}' are invalid, but user chose to run as is.");
                    return settings;
                default:
                    this.logger.LogWarning($"Settings for target '{name}' are invalid, and user cancelled the operation.");
                    return null;
            }
        }

        // 設定が存在しない、または検証エラーがある場合、設定ダイアログを開く
        if (!await presentationService.OpenAllSettingsDialogAsync(name, false))
        {
            return null;
        }

        return scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TargetSettings>>().Get(name);
    }

    private async Task OpenTargetWindowCoreAsync(IntPtr targetWindowHandle, string name)
    {
        using var l = await this.asyncLock.EnterAsync();
        var settings = await GetSettingsAsync(name);
        if (settings is null)
        {
            return;
        }

        var scope = provider.CreateScope();
        try
        {
            var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<CommonSettings>>();
            var presentationService = scope.ServiceProvider.GetRequiredService<IPresentationService>();
            var processInfo = scope.ServiceProvider.GetRequiredService<IProcessInfoStoreInternal>();
            processInfo.SetTargetProcess(targetWindowHandle, name);

            var window = options.Value.ViewMode switch
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
        catch (Exception)
        {
            scope.Dispose();
            throw;
        }
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
