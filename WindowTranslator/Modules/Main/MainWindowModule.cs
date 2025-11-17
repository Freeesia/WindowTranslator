using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using System.Collections.ObjectModel;
using WindowTranslator.Properties;
using WindowTranslator.Services;
using WindowTranslator.Stores;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Modules.Main;
public sealed class MainWindowModule(App app, IServiceProvider provider) : IMainWindowModule, IDisposable
{
    private readonly App app = app;
    private readonly IServiceProvider provider = provider;
    private readonly AsyncSemaphore asyncLock = new(1);

    public ObservableCollection<WindowInfo> OpenedWindows { get; } = new();

    public Task OpenTargetAsync(IntPtr targetWindowHandle, string name)
        => this.app.Dispatcher.Invoke(() => OpenTargetWindowCoreAsync(targetWindowHandle, name));

    private static TargetSettings GetTargetSettings(IOptionsSnapshot<UserSettings> options, string name)
    {
        return options.Value.Targets.TryGetValue(name, out var settings)
            ? settings
            : options.Value.Targets.TryGetValue(string.Empty, out var defaultSettings)
                ? defaultSettings
                : new TargetSettings();
    }

    private async Task OpenTargetWindowCoreAsync(IntPtr targetWindowHandle, string name)
    {
        using var l = await this.asyncLock.EnterAsync();
        var scope = provider.CreateScope();
        try
        {
            var options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<UserSettings>>();
            var presentationService = scope.ServiceProvider.GetRequiredService<IPresentationService>();
            var processInfo = scope.ServiceProvider.GetRequiredService<IProcessInfoStoreInternal>();
            processInfo.SetTargetProcess(targetWindowHandle, name);

            // 設定が存在しない場合、設定ダイアログを開く
            if (!options.Value.Targets.ContainsKey(name) && !await presentationService.OpenAllSettingsDialogAsync(name))
            {
                return;
            }

            // 翻訳対象の設定を取得
            var targetSettings = GetTargetSettings(options, name);
            
            // 設定を検証
            var validationService = scope.ServiceProvider.GetRequiredService<ITargetSettingsValidationService>();
            var validationResults = await validationService.ValidateAsync(name, targetSettings);
            
            if (validationResults.Any())
            {
                // 検証エラーがある場合、エラーダイアログを表示
                var dialogService = scope.ServiceProvider.GetRequiredService<IContentDialogService>();
                var result = await dialogService.ShowSimpleDialogAsync(new()
                {
                    Title = Resources.SettingsInvalid,
                    Content = string.Join("\n\n", validationResults.Select(r => $"### {r.Title}\n{r.Message}")),
                    PrimaryButtonText = Resources.Settings,
                    CloseButtonText = Resources.Cancel,
                });

                if (result == ContentDialogResult.Primary)
                {
                    // 設定ダイアログを開く（保存されるとConfigurationがリロードされる）
                    if (!await presentationService.OpenAllSettingsDialogAsync(name))
                    {
                        return;
                    }
                    
                    // 設定が保存された後、再度検証を行う（新しいscopeで最新の設定を取得）
                    scope.Dispose();
                    scope = provider.CreateScope();
                    options = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<UserSettings>>();
                    presentationService = scope.ServiceProvider.GetRequiredService<IPresentationService>();
                    processInfo = scope.ServiceProvider.GetRequiredService<IProcessInfoStoreInternal>();
                    processInfo.SetTargetProcess(targetWindowHandle, name);
                    
                    targetSettings = GetTargetSettings(options, name);
                    
                    validationService = scope.ServiceProvider.GetRequiredService<ITargetSettingsValidationService>();
                    validationResults = await validationService.ValidateAsync(name, targetSettings);
                    
                    // まだ検証エラーがある場合は翻訳を開始しない
                    if (validationResults.Any())
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
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
