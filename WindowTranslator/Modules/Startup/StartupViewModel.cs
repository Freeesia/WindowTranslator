using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Startup;

[ObservableObject]
public partial class StartupViewModel
{
    private readonly IPresentationService presentationService;
    private readonly IProcessInfoStore processInfoStore;
    private readonly IServiceProvider serviceProvider;
    [ObservableProperty]
    private IReadOnlyList<ProcessInfo> processInfos = Array.Empty<ProcessInfo>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]

    private ProcessInfo? selectedProcess;

    public StartupViewModel(IPresentationService presentationService, IProcessInfoStore processInfoStore, IServiceProvider serviceProvider)
    {
        _ = RefreshProcessAsync();
        this.presentationService = presentationService;
        this.processInfoStore = processInfoStore;
        this.serviceProvider = serviceProvider;
    }

    [RelayCommand]
    public async Task RefreshProcessAsync()
    {
        var processes = await Task.Run(() => Process.GetProcesses());
        this.ProcessInfos = processes.Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.ProcessName))
            .Select(p => new ProcessInfo(p.MainWindowTitle, p.Id, p.MainWindowHandle, p.ProcessName))
            .ToArray();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    public async Task RunAsync()
    {
        if (this.SelectedProcess is not { } p)
        {
            return;
        }
        this.processInfoStore.SetTargetProcess(p.WindowHandle, p.Name);
        var app = Application.Current;
        var window = app.MainWindow;
        try
        {
            await this.presentationService.OpenMainWindowAsync();
            app.MainWindow = app.Windows.OfType<Window>().Single(w => w.IsActive);
            window.Close();
        }
        catch (Exception ex)
        {
            app.MainWindow = window;
            this.presentationService.ShowMessage($"""
                ウィンドウの埋め込みに失敗しました。
                エラー：{ex.Message}
                """, icon: Kamishibai.MessageBoxImage.Error);
        }
    }

    public bool CanRun() => this.SelectedProcess is not null;

    [RelayCommand]
    public async Task OpenSettingsDialogAsync(object owner)
    {
        using var scope = this.serviceProvider.CreateScope();
        var ps = scope.ServiceProvider.GetRequiredService<IPresentationService>();
        await ps.OpenSettingsDialogAsync(owner, new() { WindowStartupLocation = Kamishibai.WindowStartupLocation.CenterOwner });
    }
}

public record ProcessInfo(string Title, int PID, IntPtr WindowHandle, string Name);
