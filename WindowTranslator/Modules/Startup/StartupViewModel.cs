using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Composition.WindowsRuntimeHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PInvoke;
using System.Diagnostics;
using System.Windows;
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

    public StartupViewModel(IPresentationService presentationService, IProcessInfoStore processInfoStore, IServiceProvider serviceProvider, IOptionsMonitor<UserSettings> options)
    {
        this.presentationService = presentationService;
        this.processInfoStore = processInfoStore;
        this.serviceProvider = serviceProvider;
        this.options = options;
    }

    [RelayCommand]
    public async Task RunAsync()
    {
        var app = Application.Current;
        var window = app.MainWindow;
        var picker = new GraphicsCapturePicker();
        picker.SetWindow(new WindowInteropHelper(window).Handle);
        ProcessInfo? p = null;
        while (p is null)
        {
            var item = await picker.PickSingleItemAsync();
            if (item is null)
            {
                continue;
            }
            p = FindProcessByWindowTitle(item.DisplayName);
            if (p is null)
            {
                this.presentationService.ShowMessage($"""
                選択したウィンドウ「{item.DisplayName}」はプロセスを特定できないため、キャプチャー出来ません。
                モニターはサポート対象外です。
                """, icon: Kamishibai.MessageBoxImage.Error, owner: window);
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
            app.MainWindow = app.Windows.OfType<Window>().Single(w => w.IsActive);
            window.Close();
        }
        catch (Exception ex)
        {
            app.MainWindow = window;
            this.presentationService.ShowMessage($"""
                ウィンドウの埋め込みに失敗しました。
                エラー：{ex.Message}
                """, icon: Kamishibai.MessageBoxImage.Error, owner: window);
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

