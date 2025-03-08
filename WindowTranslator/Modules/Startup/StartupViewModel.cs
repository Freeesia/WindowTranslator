using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Composition.WindowsRuntimeHelpers;
using Microsoft.Extensions.DependencyInjection;
using PInvoke;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Windows.Graphics.Capture;
using WindowTranslator.Modules.Main;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.Startup;

[ObservableObject]
public partial class StartupViewModel
{
    private readonly IPresentationService presentationService;
    private readonly IServiceProvider serviceProvider;
    private readonly IMainWindowModule mainWindowModule;
    private readonly ObservableCollection<MenuItemViewModel> attachingWindows;

    public IEnumerable<MenuItemViewModel> TaskBarIconMenus { get; }

    public StartupViewModel(IPresentationService presentationService, IServiceProvider serviceProvider, IMainWindowModule mainWindowModule)
    {
        this.presentationService = presentationService;
        this.serviceProvider = serviceProvider;
        this.mainWindowModule = mainWindowModule;
        this.attachingWindows = new(this.mainWindowModule.OpenedWindows.Select(CreateMenu));
        this.mainWindowModule.OpenedWindows.CollectionChanged += OpenedWindows_CollectionChanged;
        this.TaskBarIconMenus =
        [
            new MenuItemViewModel(Resources.Attach, this.RunCommand, []),
            new MenuItemViewModel("アタッチ中", null, this.attachingWindows),
            new MenuItemViewModel(Resources.Settings, this.OpenSettingsDialogCommand, []),
            new MenuItemViewModel(Resources.Exit, this.ExitCommand, []),
        ];
    }

    private void OpenedWindows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var item in e.NewItems!.OfType<WindowInfo>())
                {
                    this.attachingWindows.Add(CreateMenu(item));
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var item in e.OldItems!.OfType<WindowInfo>())
                {
                    var menu = this.attachingWindows.FirstOrDefault(x => x.Header == item.Name);
                    if (menu is not null)
                    {
                        this.attachingWindows.Remove(menu);
                    }
                }
                break;
        }
    }

    private MenuItemViewModel CreateMenu(WindowInfo item)
        => new(item.Name, null, [
                new(Resources.Settings, new AsyncRelayCommand(() => OpenSettingsDialogAsync(item.Name)), []),
                new(Resources.Detach, new RelayCommand(item.Window.Close), []),
            ]);

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
            else if (p.PID == Environment.ProcessId)
            {
                this.presentationService.ShowMessage($"""
                WindowTranslator以外のウィンドウを選択してください
                """, icon: Kamishibai.MessageBoxImage.Error, owner: window);
                p = null;
            }
        }
        try
        {
            await this.mainWindowModule.OpenTargetAsync(p.WindowHandle, p.Name);
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
    public async Task OpenSettingsDialogAsync(string? target)
    {
        using var scope = this.serviceProvider.CreateScope();
        var ps = scope.ServiceProvider.GetRequiredService<IPresentationService>();
        var r = await ps.OpenAllSettingsDialogAsync(target ?? string.Empty, Application.Current.MainWindow, new() { WindowStartupLocation = Kamishibai.WindowStartupLocation.CenterOwner });
        if (r && !string.IsNullOrEmpty(target))
        {
            foreach (var (_, handle, w) in this.mainWindowModule.OpenedWindows.Where(w => w.Name == target).ToArray())
            {
                w.Close();
                await this.mainWindowModule.OpenTargetAsync(handle, target);
            }
        }
    }

    [RelayCommand]
    public static void Exit()
        => Application.Current.Shutdown();

    private static ProcessInfo? FindProcessByWindowTitle(string targetTitle)
    {
        var hWnd = User32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, null);

        while (hWnd != IntPtr.Zero)
        {
            var windowTitle = User32.GetWindowText(hWnd);
            if (windowTitle == targetTitle)
            {
                _ = User32.GetWindowThreadProcessId(hWnd, out var processId);
                var p = Process.GetProcessById(processId);
                return new ProcessInfo(windowTitle, processId, hWnd, p.ProcessName);
            }

            hWnd = User32.FindWindowEx(IntPtr.Zero, hWnd, null, null);
        }
        return null;
    }

    private record ProcessInfo(string Title, int PID, IntPtr WindowHandle, string Name);
}

public record MenuItemViewModel(string Header, ICommand? Command, IReadOnlyList<MenuItemViewModel> SubCommands);