using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Windows;

namespace WindowTranslator;

[ObservableObject]
public partial class StartupViewModel
{
    private readonly IPresentationService presentationService;

    [ObservableProperty]
    private IReadOnlyList<ProcessInfo> processInfos = Array.Empty<ProcessInfo>();

    [ObservableProperty]
    [AlsoNotifyCanExecuteFor(nameof(RunCommand))]

    private IntPtr selectedWindowHandle;

    public StartupViewModel(IPresentationService presentationService)
    {
        _ = RefreshProcessAsync();
        this.presentationService = presentationService;
    }

    [ICommand]
    public async Task RefreshProcessAsync()
    {
        var processes = await Task.Run(() => Process.GetProcesses());
        this.ProcessInfos = processes.Where(p => p.MainWindowHandle != IntPtr.Zero)
            .Select(p => new ProcessInfo(p.MainWindowTitle, p.Id, p.MainWindowHandle))
            .ToArray();
    }

    [ICommand(CanExecute = nameof(CanRun))]
    public async Task RunAsync()
    {
        var app = Application.Current;
        var window = app.MainWindow;
        try
        {
            await this.presentationService.OpenMainWindowAsync(this.SelectedWindowHandle);
            app.MainWindow = app.Windows.OfType<Window>().Single(w => w.IsActive);
            window.Close();
        }
        catch
        {
            app.MainWindow = window;
            this.presentationService.ShowMessage("ウィンドウの埋め込みに失敗しました。", icon: Kamishibai.MessageBoxImage.Error);
        }
    }

    public bool CanRun() => this.SelectedWindowHandle != IntPtr.Zero;
}

public record ProcessInfo(string Title, int PID, IntPtr WindowHandle);
