using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;
using Windows.Win32.Foundation;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Modules.PluginStore;

public partial class PluginSetupWindow : FluentWindow
{
    private static readonly SafeFileHandle MarkerValue = new(new(1), ownsHandle: false);
    private readonly PluginSetupViewModel viewModel;
    private HWND windowHandle;
    private HwndSource? hwndSource;

    internal PluginSetupWindow(PluginSetupViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        this.DataContext = viewModel;
        UiApplication.Current.Resources = this.Resources;
        SystemThemeWatcher.Watch(this);
        this.viewModel.Completed += OnCompleted;
    }

    private void OnCompleted(object? sender, EventArgs e) => Close();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.windowHandle = new(new WindowInteropHelper(this).Handle);
        if (!SetProp(this.windowHandle, SingleInstanceWindowActivator.StartupDialogMarker, MarkerValue))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        this.hwndSource = HwndSource.FromHwnd(this.windowHandle);
        this.hwndSource.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (unchecked((uint)msg) == SingleInstanceWindowActivator.ActivationMessage)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.SetCurrentValue(WindowStateProperty, WindowState.Normal);
            }
            _ = Activate();
            handled = true;
        }
        return IntPtr.Zero;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!this.viewModel.IsCompleted)
        {
            // 閉じる操作はスキップとして結果を保存し、保存できるまでは画面を閉じない。
            e.Cancel = true;
            _ = this.viewModel.FinishCommand.ExecuteAsync(null);
        }
        else if (this.IsLoaded)
        {
            SystemThemeWatcher.UnWatch(this);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        this.viewModel.Completed -= OnCompleted;
        this.hwndSource?.RemoveHook(WndProc);
        if (!this.windowHandle.IsNull)
        {
            using SafeFileHandle marker = RemoveProp(this.windowHandle, SingleInstanceWindowActivator.StartupDialogMarker);
            marker.SetHandleAsInvalid();
        }
        base.OnClosed(e);
    }
}
