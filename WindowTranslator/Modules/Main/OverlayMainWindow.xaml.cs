using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using PInvoke;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using WindowTranslator.Stores;
using static Windows.Win32.PInvoke;
using static PInvoke.User32;

namespace WindowTranslator.Modules.Main;

/// <summary>
/// OverlayMainWindow.xaml の相互作用ロジック
/// </summary>
public partial class OverlayMainWindow : Window
{
    private readonly IProcessInfoStore processInfo;
    private readonly IPresentationService presentationService;
    private readonly DispatcherTimer timer = new();
    private readonly ILogger<OverlayMainWindow> logger;
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

    public OverlayMainWindow(IProcessInfoStore processInfo, IPresentationService presentationService, ILogger<OverlayMainWindow> logger)
    {
        InitializeComponent();
        this.processInfo = processInfo;
        this.presentationService = presentationService;
        this.logger = logger;
        this.timer.Interval = TimeSpan.FromMilliseconds(10);
        this.timer.Tick += (s, e) => UpdateWindowPositionAndSize();
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
        var extendedStyle = (SetWindowLongFlags)GetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE);
        var r = SetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE, extendedStyle | SetWindowLongFlags.WS_EX_TRANSPARENT | SetWindowLongFlags.WS_EX_TOOLWINDOW);
        if (r == 0)
        {
            this.logger.LogError($"SetWindowLong failed. {Marshal.GetLastWin32Error()}");
        }

        // ShowInTaskbarをfalseにすると↓の方法で一番上に表示する必要がある
        // https://social.msdn.microsoft.com/Forums/en-US/cdbe457f-d653-4a18-9295-bb9b609bc4e3/desktop-apps-on-top-of-metro-extended
        IntPtr hWndHiddenOwner = User32.GetWindow(this.windowHandle, GetWindowCommands.GW_OWNER);
        SetWindowPos(hWndHiddenOwner, new(-1), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        // 2回呼ばないと安定して最上位にならない
        SetWindowPos(hWndHiddenOwner, new(-1), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        this.timer.Start();

        RegisterHotKey(new(this.windowHandle), 0, HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.O));
        var source = HwndSource.FromHwnd(this.windowHandle);
        source.AddHook(WndProc);

    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        this.timer.Stop();
    }

    private unsafe void UpdateWindowPositionAndSize()
    {
        var sw = Stopwatch.StartNew();
        var windowInfo = WINDOWINFO.Create();
        if (!GetWindowInfo(this.processInfo.MainWindowHandle, ref windowInfo))
        {
            this.timer.Stop();
            this.presentationService.CloseWindowAsync(this).Forget();
            return;
        }

        var monitorHandle = MonitorFromWindow(this.processInfo.MainWindowHandle, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        GetMonitorInfo(monitorHandle, out var monitorInfo);
        var mode = DEVMODE.Create();
        EnumDisplaySettings(monitorInfo.DeviceName, ENUM_CURRENT_SETTINGS, &mode);
        var eDpiScale = GetDpiForSystem() / 96.0;
        var rDpiScale = eDpiScale * mode.dmPelsWidth / (monitorInfo.Monitor.right - monitorInfo.Monitor.left);

        var clientRect = windowInfo.rcClient;
        var windowRect = windowInfo.rcWindow;

        var p = GetWindowPlacement(this.processInfo.MainWindowHandle);

        var left = clientRect.left;
        var top = p.showCmd.HasFlag(WindowShowStyle.SW_MAXIMIZE) ? clientRect.top : windowRect.top;
        var width = clientRect.right - left;
        var height = clientRect.bottom - top;

        var nativePos = GetCursorPos();
        var x = (nativePos.x - left) / eDpiScale;
        var y = (nativePos.y - top) / eDpiScale;

        this.SetCurrentValue(ScaleProperty, 1 / rDpiScale);
        this.SetCurrentValue(LeftProperty, left / eDpiScale);
        this.SetCurrentValue(TopProperty, top / eDpiScale);
        this.SetCurrentValue(WidthProperty, width / eDpiScale);
        this.SetCurrentValue(HeightProperty, height / eDpiScale);
        this.SetCurrentValue(MousePosProperty, new Point(x, y));
        this.logger.LogDebug($"Window: (x:{left:f2}, y:{top:f2}, w:{width:f2}, h:{height:f2}), マウス位置：({x:f2}, {y:f2} {sw.Elapsed}");
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY)
        {
            return 0;
        }

        HideOverlay();
        return 0;
    }

    private async void HideOverlay()
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
