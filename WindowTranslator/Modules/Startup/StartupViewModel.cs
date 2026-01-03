using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Composition.WindowsRuntimeHelpers;
using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Windows.Graphics.Capture;
using WindowTranslator.Extensions;
using WindowTranslator.Modules.Main;
using WindowTranslator.Properties;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Modules.Startup;

[ObservableObject]
public partial class StartupViewModel
{
    private readonly IPresentationService presentationService;
    private readonly IServiceProvider serviceProvider;
    private readonly IMainWindowModule mainWindowModule;
    private readonly IVirtualDesktopManager desktopManager;
    private readonly ObservableCollection<MenuItemViewModel> attachingWindows;
    private IWindow? logView;

    public IEnumerable<MenuItemViewModel> TaskBarIconMenus { get; }

    public StartupViewModel(IPresentationService presentationService, IServiceProvider serviceProvider, IMainWindowModule mainWindowModule, IVirtualDesktopManager desktopManager)
    {
        this.presentationService = presentationService;
        this.serviceProvider = serviceProvider;
        this.mainWindowModule = mainWindowModule;
        this.desktopManager = desktopManager;
        this.attachingWindows = new(this.mainWindowModule.OpenedWindows.Select(CreateMenu));
        this.mainWindowModule.OpenedWindows.CollectionChanged += OpenedWindows_CollectionChanged;
        this.TaskBarIconMenus =
        [
            new MenuItemViewModel(Resources.Attach, this.RunCommand, []),
            new MenuItemViewModel(Resources.Attaching, null, this.attachingWindows),
            new MenuItemViewModel(Resources.Settings, this.OpenSettingsDialogCommand, []),
            new MenuItemViewModel(Resources.Log, this.OpenLogWindowCommand, []),
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
            p = FindProcessByWindowTitle(item.DisplayName, item.Size);
            if (p is null)
            {
                this.presentationService.ShowMessage(string.Format(Resources.UnknownWindow, item.DisplayName), icon: Kamishibai.MessageBoxImage.Error, owner: window);
            }
            else if (p.PID == Environment.ProcessId)
            {
                this.presentationService.ShowMessage(Resources.SelectOtherWindow, icon: Kamishibai.MessageBoxImage.Error, owner: window);
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
            await this.presentationService.OpenErrorDialogAsync(Resources.FaildOverlay, ex, p.Name, string.Empty, window);
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
        await ps.OpenAllSettingsDialogAsync(target ?? string.Empty, null, Application.Current.MainWindow, new() { WindowStartupLocation = Kamishibai.WindowStartupLocation.CenterOwner });
    }

    [RelayCommand]
    private async Task OpenLogWindowAsync()
    {
        if (this.logView?.IsClosed ?? true)
        {
            this.logView = await this.presentationService.OpenLogWindowAsync(Application.Current.MainWindow, new() { WindowStartupLocation = Kamishibai.WindowStartupLocation.CenterOwner });
        }
        if (this.logView.WindowState == Kamishibai.WindowState.Minimized)
        {
            this.logView.Restore();
        }
        this.logView.Activate();
    }

    [RelayCommand]
    public static void Exit()
        => Application.Current.Shutdown();

    private ProcessInfo? FindProcessByWindowTitle(string targetTitle, Windows.Graphics.SizeInt32 targetSize)
    {
        ProcessInfo? result = null;
        ProcessInfo? candidate = null;

        EnumWindows((hWnd, _) =>
        {
            if (IsIgnoreWindow(hWnd) || !this.desktopManager.IsWindowOnCurrentVirtualDesktop(hWnd))
            {
                return true;
            }

            var windowTitle = GetWindowText(hWnd);
            if (windowTitle != targetTitle)
            {
                return true;
            }

            if (GetWindowThreadProcessId(hWnd, out var processId) == 0)
            {
                return true;
            }

            Process p;
            try
            {
                p = Process.GetProcessById(unchecked((int)processId));
            }
            catch (ArgumentException)
            {
                return true;
            }

            // ウィンドウサイズを取得
            var (width, height) = GetWindowSizeForWgcCompare(hWnd);
            // サイズが完全一致する場合は即座に結果を設定して終了
            if (width == targetSize.Width && height == targetSize.Height)
            {
                result = new ProcessInfo(windowTitle, p.Id, hWnd, p.ProcessName);
                return false; // 列挙を終了
            }

            // タイトルは一致するが、サイズが異なる場合は候補として保持
            candidate ??= new ProcessInfo(windowTitle, p.Id, hWnd, p.ProcessName);

            return true;
        }, IntPtr.Zero);

        // 完全一致が見つかった場合はそれを返し、そうでなければ候補を返す
        return result ?? candidate;
    }

    private record ProcessInfo(string Title, int PID, IntPtr WindowHandle, string Name);
}

public record MenuItemViewModel(string Header, ICommand? Command, IReadOnlyList<MenuItemViewModel> SubCommands);