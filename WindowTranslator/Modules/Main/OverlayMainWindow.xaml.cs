using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowTranslator.Controls;
using WindowTranslator.Extensions;
using WindowTranslator.Stores;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Modules.Main;

/// <summary>
/// OverlayMainWindow.xaml の相互作用ロジック
/// </summary>
public partial class OverlayMainWindow : Window
{
    private readonly OverlaySwitch overlaySwitch;
    private readonly bool isEnableCapture;
    private readonly IProcessInfoStore processInfo;
    private readonly IVirtualDesktopManager desktopManager;
    private readonly DispatcherTimer timer = new();
    private readonly ILogger<OverlayMainWindow> logger;
    private readonly HOT_KEY_MODIFIERS shortcutModifiers;
    private readonly int shortcutKey;
    private IntPtr windowHandle;
    private int overlayHiddenCount;

    public Point MousePos
    {
        get => (Point)GetValue(MousePosProperty);
        set => SetValue(MousePosProperty, value);
    }

    /// <summary>Identifies the <see cref="MousePos"/> dependency property.</summary>
    public static readonly DependencyProperty MousePosProperty =
        DependencyProperty.Register(nameof(MousePos), typeof(Point), typeof(OverlayMainWindow), new PropertyMetadata(new Point(double.NaN, double.NaN)));

    public double Scale
    {
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Identifies the <see cref="Scale"/> dependency property.</summary>
    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.Register(nameof(Scale), typeof(double), typeof(OverlayMainWindow), new PropertyMetadata(1.0));

    public bool IsSwapVisibility
    {
        get => (bool)GetValue(IsSwapVisibilityProperty);
        set => SetValue(IsSwapVisibilityProperty, value);
    }

    /// <summary>Identifies the <see cref="IsSwapVisibility"/> dependency property.</summary>
    public static readonly DependencyProperty IsSwapVisibilityProperty =
        DependencyProperty.Register(nameof(IsSwapVisibility), typeof(bool), typeof(OverlayMainWindow), new PropertyMetadata(false));

    public OverlayMainWindow(
        IOptionsSnapshot<CommonSettings> settings,
        IOptionsSnapshot<TargetSettings> targetSettings,
        IProcessInfoStore processInfo,
        IVirtualDesktopManager desktopManager,
        ILogger<OverlayMainWindow> logger)
    {
        InitializeComponent();
        this.overlaySwitch = settings.Value.OverlaySwitch;
        this.isEnableCapture = settings.Value.IsEnableCaptureOverlay;
        this.IsSwapVisibility = settings.Value.IsOverlayPointSwap;
        this.processInfo = processInfo;
        this.desktopManager = desktopManager;
        this.logger = logger;
        this.timer.Interval = TimeSpan.FromMilliseconds(10);
        this.timer.Tick += (s, e) => UpdateWindowPositionAndSize();

        // Configure the shortcut modifiers based on settings
        (this.shortcutModifiers, this.shortcutKey) = targetSettings.Value.OverlayShortcut.ToHotKey();
#if false
        var brush = System.Windows.Media.Brushes.Red.Clone();
        brush.Opacity = 0.2;
        this.Background = brush;
        this.Opacity = 0.8;
#endif
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.windowHandle = new WindowInteropHelper(this).Handle;

        // ディスプレイの場合は仮想デスクトップチェックをスキップ
        if (!this.processInfo.Name.StartsWith("DISPLAY__", StringComparison.OrdinalIgnoreCase))
        {
            if (!this.desktopManager.IsWindowOnCurrentVirtualDesktop(this.processInfo.MainWindowHandle))
            {
                var targetDesktop = this.desktopManager.GetWindowDesktopId(this.processInfo.MainWindowHandle);
                this.desktopManager.MoveWindowToDesktop(this.windowHandle, ref targetDesktop);
            }
        }

        var extendedStyle = (WINDOW_EX_STYLE)GetWindowLong(new(windowHandle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) | WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
        if (!this.isEnableCapture)
        {
            extendedStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        }
        var r = SetWindowLong(new(windowHandle), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, extendedStyle);
        if (r == 0)
        {
            this.logger.LogError($"SetWindowLong failed. {Marshal.GetLastWin32Error()}");
        }

        // ShowInTaskbarをfalseにすると↓の方法で一番上に表示する必要がある
        // https://social.msdn.microsoft.com/Forums/en-US/cdbe457f-d653-4a18-9295-bb9b609bc4e3/desktop-apps-on-top-of-metro-extended
        var hWndHiddenOwner = Windows.Win32.PInvoke.GetWindow(new(this.windowHandle), GET_WINDOW_CMD.GW_OWNER);
        SetWindowPos(hWndHiddenOwner, HWND.HWND_TOPMOST, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        // 2回呼ばないと安定して最上位にならない
        SetWindowPos(hWndHiddenOwner, HWND.HWND_TOPMOST, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        this.timer.Start();

        RegisterHotKey(new(this.windowHandle), 0, this.shortcutModifiers, (uint)this.shortcutKey);
        var source = HwndSource.FromHwnd(this.windowHandle);
        source.AddHook(WndProc);

        StrongReferenceMessenger.Default.Register<OverlayMainWindow, CloseMessage>(this, CloseIfViewModel);
    }

    private static void CloseIfViewModel(OverlayMainWindow w, CloseMessage m)
        => _ = w.Dispatcher.BeginInvoke(static (OverlayMainWindow w, CloseMessage m) =>
        {
            if (w.DataContext == m.ViewModel)
            {
                w.Close();
            }
        }, w, m);

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        this.timer.Stop();
        UnregisterHotKey(new(this.windowHandle), 0);
        StrongReferenceMessenger.Default.Unregister<CloseMessage>(this);
    }

    private unsafe void UpdateWindowPositionAndSize()
    {
        var sw = Stopwatch.StartNew();
        
        // ディスプレイの場合は専用の処理
        if (this.processInfo.Name.StartsWith("DISPLAY__", StringComparison.OrdinalIgnoreCase))
        {
            UpdateDisplayPositionAndSize();
            return;
        }
        
        var windowInfo = new WINDOWINFO() { cbSize = (uint)Marshal.SizeOf<WINDOWINFO>() };
        if (!GetWindowInfo(new(this.processInfo.MainWindowHandle), ref windowInfo))
        {
            this.timer.Stop();
            this.Close();
            return;
        }

        if (!this.desktopManager.IsWindowOnCurrentVirtualDesktop(this.processInfo.MainWindowHandle))
        {
            this.SetCurrentValue(VisibilityProperty, Visibility.Hidden);
            return;
        }

        var clientRect = windowInfo.rcClient;
        var windowRect = windowInfo.rcWindow;

        // 対象のウィンドウの中心位置が他のウィンドウによって隠れているかチェック
        var windowAtPoint = WindowFromPoint(new((clientRect.left + clientRect.right) / 2, (clientRect.top + clientRect.bottom) / 2));
        // ウィンドウの中心が別のウィンドウに隠されている場合は非表示にする
        if (windowAtPoint != this.processInfo.MainWindowHandle && !IsChild(new(this.processInfo.MainWindowHandle), windowAtPoint))
        {
            this.SetCurrentValue(VisibilityProperty, Visibility.Hidden);
            return;
        }

        // 上記のすべてのチェックに合格した場合、オーバーレイを表示
        this.SetCurrentValue(VisibilityProperty, Visibility.Visible);

        // 本気のフルスクリーンだと何かの拍子に裏側に行ってしまうので、定期的に最前面に持ってくる
        var hWndHiddenOwner = Windows.Win32.PInvoke.GetWindow(new(this.windowHandle), GET_WINDOW_CMD.GW_OWNER);
        SetWindowPos(hWndHiddenOwner, new(-1), 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        var monitorHandle = MonitorFromWindow(new(this.processInfo.MainWindowHandle), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = default(MONITORINFOEXW);
        monitorInfo.monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>();
        GetMonitorInfo(monitorHandle, ref monitorInfo.monitorInfo);
        var mode = default(DEVMODEW);
        EnumDisplaySettings(monitorInfo.szDevice.ToString(), ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode);
        var eDpiScale = GetDpiForSystem() / 96.0;
        var rDpiScale = eDpiScale * mode.dmPelsWidth / (monitorInfo.monitorInfo.rcMonitor.right - monitorInfo.monitorInfo.rcMonitor.left);

        var p = default(WINDOWPLACEMENT);
        GetWindowPlacement(new(this.processInfo.MainWindowHandle), ref p);

        var left = clientRect.left;
        var top = p.showCmd.HasFlag(SHOW_WINDOW_CMD.SW_MAXIMIZE) ? monitorInfo.monitorInfo.rcWork.top : windowRect.top;
        var width = clientRect.right - left;
        var height = clientRect.bottom - top;

        GetCursorPos(out var nativePos);
        var x = (nativePos.X - left) / eDpiScale;
        var y = (nativePos.Y - top) / eDpiScale;

        this.logger.LogDebug($"Window: (x:{left:f2}, y:{top:f2}, w:{width:f2}, h:{height:f2}), マウス位置：({x:f2}, {y:f2} {sw.Elapsed}");
        this.SetCurrentValue(MousePosProperty, new Point(x, y));
        if (this.isEnableCapture && p.showCmd == SHOW_WINDOW_CMD.SW_SHOWMINIMIZED)
        {
            return;
        }
        this.SetCurrentValue(ScaleProperty, 1 / rDpiScale);
        this.SetCurrentValue(LeftProperty, left / eDpiScale);
        this.SetCurrentValue(TopProperty, top / eDpiScale);
        this.SetCurrentValue(WidthProperty, width / eDpiScale);
        this.SetCurrentValue(HeightProperty, height / eDpiScale);
    }

    private unsafe void UpdateDisplayPositionAndSize()
    {
        // モニターハンドルを取得（IntPtrとして既に持っている）
        var monitorHandle = this.processInfo.MainWindowHandle;
        var monitorInfo = default(MONITORINFOEXW);
        monitorInfo.monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>();
        
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo.monitorInfo))
        {
            this.logger.LogWarning("Failed to get monitor info");
            return;
        }
        
        var left = monitorInfo.monitorInfo.rcMonitor.left;
        var top = monitorInfo.monitorInfo.rcMonitor.top;
        var width = monitorInfo.monitorInfo.rcMonitor.right - left;
        var height = monitorInfo.monitorInfo.rcMonitor.bottom - top;
        
        // モニター座標の検証
        if (width <= 0 || height <= 0)
        {
            this.logger.LogWarning($"Invalid monitor dimensions: {width}x{height}");
            return;
        }
        
        // モニターの解像度情報を取得
        var mode = default(DEVMODEW);
        var eDpiScale = GetDpiForSystem() / 96.0;
        var rDpiScale = eDpiScale;
        
        if (EnumDisplaySettings(monitorInfo.szDevice.ToString(), ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode))
        {
            // EnumDisplaySettings が成功した場合のみ rDpiScale を計算
            if (mode.dmPelsWidth > 0)
            {
                rDpiScale = eDpiScale * mode.dmPelsWidth / width;
            }
        }
        else
        {
            this.logger.LogWarning("Failed to get display settings, using default DPI scale");
        }
        
        GetCursorPos(out var nativePos);
        var x = (nativePos.X - left) / eDpiScale;
        var y = (nativePos.Y - top) / eDpiScale;
        
        this.logger.LogDebug($"Display: (x:{left:f2}, y:{top:f2}, w:{width:f2}, h:{height:f2}), マウス位置：({x:f2}, {y:f2})");
        this.SetCurrentValue(MousePosProperty, new Point(x, y));
        this.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        this.SetCurrentValue(ScaleProperty, 1 / rDpiScale);
        this.SetCurrentValue(LeftProperty, left / eDpiScale);
        this.SetCurrentValue(TopProperty, top / eDpiScale);
        this.SetCurrentValue(WidthProperty, width / eDpiScale);
        this.SetCurrentValue(HeightProperty, height / eDpiScale);
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
        this.overlay.SetCurrentValue(VisibilityProperty, Visibility.Hidden);
        await Task.Delay(500);
        if (Interlocked.CompareExchange(ref this.overlayHiddenCount, 0, current) == current)
        {
            this.overlay.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        }
    }
}
