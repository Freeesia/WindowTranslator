using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Composition.WindowsRuntimeHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PInvoke;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Windows.Graphics.Capture;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Startup;

[ObservableObject]
public partial class StartupViewModel
{
    private readonly IPresentationService presentationService;
    private readonly IProcessInfoStore processInfoStore;
    private readonly IServiceProvider serviceProvider;
    private readonly IOptionsMonitor<UserSettings> options;

    public IEnumerable<MenuItemViewModel> TaskBarIconMenus { get; }

    public StartupViewModel(IPresentationService presentationService, IProcessInfoStore processInfoStore, IServiceProvider serviceProvider, IOptionsMonitor<UserSettings> options)
    {
        this.presentationService = presentationService;
        this.processInfoStore = processInfoStore;
        this.serviceProvider = serviceProvider;
        this.options = options;
        this.TaskBarIconMenus = new[]
        {
            new MenuItemViewModel("アタッチ", this.RunCommand),
            new MenuItemViewModel("設定", this.OpenSettingsDialogCommand),
            new MenuItemViewModel("終了", this.ExitCommand),
        };
    }

    [RelayCommand]
    public async Task RunAsync()
    {
        var app = Application.Current;
        var window = app.MainWindow;
        var beforeVisible = window.IsVisible;
        if (!beforeVisible)
        {
            window.Show();
        }
        var picker = new GraphicsCapturePicker();
        var handle = new WindowInteropHelper(window).Handle;
        picker.SetWindow(handle);
        ProcessInfo? p = null;
        while (p is null)
        {
            var item = await picker.PickSingleItemAsync();
            if (item is null)
            {
                if (!beforeVisible)
                {
                    window.Close();
                }
                return;
            }
            p = FindProcessByWindowTitle(item.DisplayName);
            if (p is null)
            {
                this.presentationService.ShowMessage($"""
                選択したウィンドウ「{item.DisplayName}」はプロセスを特定できないため、キャプチャー出来ません。
                モニターはサポート対象外です。
                """, icon: Kamishibai.MessageBoxImage.Error, owner: window);
            }
            else if (p.WindowHandle == handle)
            {
                this.presentationService.ShowMessage($"""
                WindowTranslator以外のウィンドウを選択してください
                """, icon: Kamishibai.MessageBoxImage.Error, owner: window);
                p = null;
            }
        }
        this.processInfoStore.SetTargetProcess(p.WindowHandle, p.Name);
        try
        {
            switch (this.options.CurrentValue.ViewMode)
            {
                case ViewMode.Capture:
                    await this.presentationService.OpenCaptureMainWindowAsync();
                    break;
                case ViewMode.Overlay:
                    await this.presentationService.OpenOverlayMainWindowAsync();
                    break;
                default:
                    throw new NotSupportedException();
            }
            window.Close();
        }
        catch (Exception ex)
        {
            this.presentationService.ShowMessage($"""
                ウィンドウの埋め込みに失敗しました。
                エラー：{ex.Message}
                """, icon: Kamishibai.MessageBoxImage.Error, owner: window);
        }
        if (!beforeVisible)
        {
            window.Close();
        }
    }

    [RelayCommand]
    public async Task OpenSettingsDialogAsync()
    {
        using var scope = this.serviceProvider.CreateScope();
        var ps = scope.ServiceProvider.GetRequiredService<IPresentationService>();
        var app = Application.Current;
        var window = app.MainWindow;
        await ps.OpenSettingsDialogAsync(window, new() { WindowStartupLocation = Kamishibai.WindowStartupLocation.CenterOwner });
    }

    [RelayCommand]
    public void Exit()
        => Application.Current.Shutdown();

    private static ProcessInfo? FindProcessByWindowTitle(string windowTitle)
    {
        var hWnd = User32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, null);

        while (hWnd != IntPtr.Zero)
        {
            int length = User32.GetWindowTextLength(hWnd);
            if (length > 0)
            {
#pragma warning disable CA2014 // ループ外に出ないので大丈夫なはず。
                Span<char> text = stackalloc char[length + 1];
#pragma warning restore CA2014
                User32.GetWindowText(hWnd, text);
                if (text[..^1].SequenceEqual(windowTitle))
                {
                    User32.GetWindowThreadProcessId(hWnd, out var processId);
                    var p = Process.GetProcessById(processId);
                    return new ProcessInfo(text.ToString(), processId, hWnd, p.ProcessName);
                }
            }

            hWnd = User32.FindWindowEx(IntPtr.Zero, hWnd, null, null);
        }
        return null;
    }

    private record ProcessInfo(string Title, int PID, IntPtr WindowHandle, string Name);
}

public record MenuItemViewModel(string Header, ICommand Command);