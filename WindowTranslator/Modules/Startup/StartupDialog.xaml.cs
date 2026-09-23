using Microsoft.Extensions.Configuration;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32.Foundation;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Modules.Startup;
/// <summary>
/// StartupDialog.xaml の相互作用ロジック
/// </summary>
public partial class StartupDialog : FluentWindow
{
    private static readonly SafeFileHandle StartupDialogMarkerValue = new(new(1), ownsHandle: false);
    private readonly LaunchMode mode;
    private HWND windowHandle;
    private HwndSource? hwndSource;
    private bool activationRequested;

    public StartupDialog(IConfiguration configuration)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.mode = configuration.GetValue(nameof(LaunchMode), LaunchMode.Direct);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        this.windowHandle = new(new WindowInteropHelper(this).Handle);
        if (!SetProp(this.windowHandle, SingleInstanceWindowActivator.StartupDialogMarker, StartupDialogMarkerValue))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        this.hwndSource = HwndSource.FromHwnd(this.windowHandle);
        this.hwndSource.AddHook(WndProc);
    }

    protected override void OnClosed(EventArgs e)
    {
        this.hwndSource?.RemoveHook(WndProc);
        if (!this.windowHandle.IsNull)
        {
            using SafeFileHandle marker = RemoveProp(this.windowHandle, SingleInstanceWindowActivator.StartupDialogMarker);
            marker.SetHandleAsInvalid();
        }
        base.OnClosed(e);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.activationRequested)
        {
            ActivateStartupDialog();
        }
        else if (this.mode == LaunchMode.Startup)
        {
            this.SetCurrentValue(VisibilityProperty, Visibility.Hidden);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (unchecked((uint)msg) == SingleInstanceWindowActivator.ActivationMessage)
        {
            this.activationRequested = true;
            if (this.IsLoaded)
            {
                ActivateStartupDialog();
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ActivateStartupDialog()
    {
        this.activationRequested = false;
        Show();
        if (this.WindowState == WindowState.Minimized)
        {
            this.SetCurrentValue(WindowStateProperty, WindowState.Normal);
        }
        _ = Activate();
    }
}

public enum LaunchMode
{
    Direct,
    Startup,
}
