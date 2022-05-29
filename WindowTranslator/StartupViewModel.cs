using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Windows;
using WindowTranslator.Stores;

namespace WindowTranslator;

[ObservableObject]
public partial class StartupViewModel
{
    private readonly IPresentationService presentationService;
    private readonly IProcessInfoStore processInfoStore;
    [ObservableProperty]
    private IReadOnlyList<ProcessInfo> processInfos = Array.Empty<ProcessInfo>();

    [ObservableProperty]
    [AlsoNotifyCanExecuteFor(nameof(RunCommand))]

    private ProcessInfo? selectedProcess;

    public StartupViewModel(IPresentationService presentationService, IProcessInfoStore processInfoStore)
    {
        _ = RefreshProcessAsync();
        this.presentationService = presentationService;
        this.processInfoStore = processInfoStore;
    }

    [ICommand]
    public async Task RefreshProcessAsync()
    {
        var processes = await Task.Run(() => Process.GetProcesses());
        this.ProcessInfos = processes.Where(p => p.MainWindowHandle != IntPtr.Zero && p.MainModule is { FileName: not null })
            .Select(p => new ProcessInfo(p.MainWindowTitle, p.Id, p.MainWindowHandle, p.MainModule!.FileName!))
            .ToArray();
    }

    [ICommand(CanExecute = nameof(CanRun))]
    public async Task RunAsync()
    {
        if (this.selectedProcess is null)
        {
            return;
        }
        this.processInfoStore.SetTargetProcess(this.selectedProcess.WindowHandle, this.selectedProcess.Path);
        var app = Application.Current;
        var window = app.MainWindow;
        try
        {
            await this.presentationService.OpenMainWindowAsync();
            app.MainWindow = app.Windows.OfType<Window>().Single(w => w.IsActive);
            window.Close();
        }
        catch
        {
            app.MainWindow = window;
            this.presentationService.ShowMessage("ウィンドウの埋め込みに失敗しました。", icon: Kamishibai.MessageBoxImage.Error);
        }
    }

    public bool CanRun() => this.SelectedProcess is not null;
}

public record ProcessInfo(string Title, int PID, IntPtr WindowHandle, string Path);
