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
    private readonly IProcessInfoStore processInfo;
    private readonly DispatcherTimer timer = new();
    private readonly bool isOneShotMode;
    private readonly HOT_KEY_MODIFIERS shortcutModifiers;
    private readonly int shortcutKey;
    private IntPtr windowHandle;

    public CaptureMainWindow(IProcessInfoStore processInfo, IOptionsSnapshot<TargetSettings> targetSettings)
    {
        InitializeComponent();
        this.processInfo = processInfo;
        this.isOneShotMode = targetSettings.Value.IsOneShotMode;
        (this.shortcutModifiers, this.shortcutKey) = targetSettings.Value.OverlayShortcut.ToHotKey();
        this.timer.Interval = TimeSpan.FromMilliseconds(10);
        this.timer.Tick += (s, e) => CheckTargetWindow();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.timer.Start();
        if (this.isOneShotMode)
        {
            this.windowHandle = new WindowInteropHelper(this).Handle;
            RegisterHotKey(new(this.windowHandle), 0, this.shortcutModifiers, (uint)this.shortcutKey);
            HwndSource.FromHwnd(this.windowHandle).AddHook(WndProc);
        }
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
        if (this.isOneShotMode)
        {
            UnregisterHotKey(new(this.windowHandle), 0);
        }
        StrongReferenceMessenger.Default.Unregister<CloseMessage>(this);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY)
        {
            return 0;
        }
        if (this.DataContext is CaptureMainViewModel viewModel)
        {
            viewModel.RequestOneShot();
        }
        return 0;
    }

    private static void CloseIfViewModel(CaptureMainWindow w, CloseMessage m)
    {
        if (w.DataContext == m.ViewModel)
        {
            w.Close();
        }
    }
}
