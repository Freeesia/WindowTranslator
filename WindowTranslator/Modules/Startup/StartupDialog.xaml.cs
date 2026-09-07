using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32.Foundation;
using WindowTranslator.Modules.PluginStore;
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
    private readonly IServiceProvider serviceProvider;
    private HWND windowHandle;
    private HwndSource? hwndSource;
    private bool activationRequested;
    private bool setupInProgress;

    public StartupDialog(
        IConfiguration configuration,
        NuGetPluginService pluginService,
        IServiceProvider serviceProvider)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.mode = configuration.GetValue(nameof(LaunchMode), LaunchMode.Direct);
        this.serviceProvider = serviceProvider;
        this.setupInProgress = pluginService.IsSetupRequired;
        if (this.setupInProgress)
        {
            this.StartupContent.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            this.SetCurrentValue(WidthProperty, Math.Min(760, SystemParameters.WorkArea.Width));
            this.SetCurrentValue(HeightProperty, Math.Min(680, SystemParameters.WorkArea.Height));
        }
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
        if (!this.setupInProgress)
        {
            Hide();
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.setupInProgress)
        {
            using var viewModel = this.serviceProvider.GetRequiredService<PluginSetupViewModel>();
            var dialog = new PluginSetupDialog(viewModel) { DialogHost = this.SetupHost };
            await dialog.ShowAsync();
            this.setupInProgress = false;
            if (viewModel.RequiresRestart)
            {
                ApplicationRestart.Restart();
                return;
            }
            this.SetCurrentValue(WidthProperty, 240.0);
            this.SetCurrentValue(HeightProperty, 168.0);
            this.StartupContent.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        }
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
