using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WindowTranslator;

[ObservableObject]
public partial class StartupViewModel
{
    [ObservableProperty]
    private IReadOnlyList<ProcessInfo> processInfos = Array.Empty<ProcessInfo>();

    [ObservableProperty]
    [AlsoNotifyCanExecuteFor(nameof(RunCommand))]

    private IntPtr selectedWindowHandle;

    public StartupViewModel()
    {
        RefreshProcess();
    }

    [ICommand]
    public async void RefreshProcess()
    {
        var processes = await Task.Run(() => Process.GetProcesses());
        this.ProcessInfos = processes.Where(p => p.MainWindowHandle != IntPtr.Zero)
            .Select(p => new ProcessInfo(p.MainWindowTitle, p.Id, p.MainWindowHandle))
            .ToArray();
    }

    [ICommand(CanExecute = nameof(CanRun))]
    public void Run()
    {
        var app = Application.Current;
        var window = app.MainWindow;
        try
        {
            app.MainWindow = new MainWindow(this.SelectedWindowHandle);
            app.MainWindow.Show();
            window.Close();
        }
        catch
        {
            app.MainWindow = window;
            MessageBox.Show(window, "ウィンドウの埋め込みに失敗しました。", "WindowTranslator", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public bool CanRun() => this.SelectedWindowHandle != IntPtr.Zero;
}

public record ProcessInfo(string Title, int PID, IntPtr WindowHandle);
