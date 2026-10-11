using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Options;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowTranslator.Extensions;
using WindowTranslator.Stores;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Modules.Main;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class CaptureMainWindow
{
    private readonly OverlaySwitch overlaySwitch;
    private readonly bool isOneShotMode;
    private readonly IProcessInfoStore processInfo;
    private readonly DispatcherTimer timer = new();
    private readonly HOT_KEY_MODIFIERS shortcutModifiers;
    private readonly int shortcutKey;
    private IntPtr windowHandle;
    private int overlayHiddenCount;

    public CaptureMainWindow(
        IOptionsSnapshot<CommonSettings> settings,
        IOptionsSnapshot<TargetSettings> targetSettings,
        IProcessInfoStore processInfo)
    {
        InitializeComponent();
        this.overlaySwitch = settings.Value.OverlaySwitch;
        this.isOneShotMode = targetSettings.Value.IsOneShotMode;
        if (this.isOneShotMode)
        {
            this.overlay.SetCurrentValue(VisibilityProperty, Visibility.Hidden);
        }
        this.processInfo = processInfo;
        this.timer.Interval = TimeSpan.FromMilliseconds(10);
        this.timer.Tick += (s, e) => CheckTargetWindow();
        (this.shortcutModifiers, this.shortcutKey) = targetSettings.Value.OverlayShortcut.ToHotKey();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.windowHandle = new WindowInteropHelper(this).Handle;
        this.timer.Start();
        RegisterHotKey(new(this.windowHandle), 0, this.shortcutModifiers, (uint)this.shortcutKey);
        HwndSource.FromHwnd(this.windowHandle).AddHook(WndProc);
        StrongReferenceMessenger.Default.Register<CaptureMainWindow, CloseMessage>(this, CloseIfViewModel);
    }

    private void CheckTargetWindow()
    {
        var windowInfo = new WINDOWINFO() { cbSize = (uint)Marshal.SizeOf<WINDOWINFO>() };
        if (!GetWindowInfo((HWND)this.processInfo.MainWindowHandle, ref windowInfo))
        {
            this.Close();
            return;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        this.timer.Stop();
        UnregisterHotKey(new(this.windowHandle), 0);
        StrongReferenceMessenger.Default.Unregister<CloseMessage>(this);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY)
        {
            return 0;
        }
        if (this.overlaySwitch == OverlaySwitch.Hold)
        {
            HoldHideOverlay();
        }
        else
        {
            this.overlay.SetCurrentValue(VisibilityProperty, this.overlay.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible);
        }
        return 0;
    }

    private async void HoldHideOverlay()
    {
        var current = Interlocked.Increment(ref this.overlayHiddenCount);
        this.overlay.SetCurrentValue(VisibilityProperty, this.isOneShotMode ? Visibility.Visible : Visibility.Hidden);
        await Task.Delay(500);
        if (Interlocked.CompareExchange(ref this.overlayHiddenCount, 0, current) == current)
        {
            this.overlay.SetCurrentValue(VisibilityProperty, this.isOneShotMode ? Visibility.Hidden : Visibility.Visible);
        }
    }

    private static void CloseIfViewModel(CaptureMainWindow w, CloseMessage m)
    {
        if (w.DataContext == m.ViewModel)
        {
            w.Close();
        }
    }
}
