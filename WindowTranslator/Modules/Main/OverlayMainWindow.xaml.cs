﻿using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PInvoke;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using WindowTranslator.Stores;
using static Windows.Win32.PInvoke;
using static PInvoke.User32;
using CommunityToolkit.Mvvm.Messaging;
using WindowTranslator.Extensions;

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

        if (!this.desktopManager.IsWindowOnCurrentVirtualDesktop(this.processInfo.MainWindowHandle))
        {
            var targetDesktop = this.desktopManager.GetWindowDesktopId(this.processInfo.MainWindowHandle);
            this.desktopManager.MoveWindowToDesktop(this.windowHandle, ref targetDesktop);
        }

        var extendedStyle = (SetWindowLongFlags)GetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE) | SetWindowLongFlags.WS_EX_TRANSPARENT;
        if (!this.isEnableCapture)
        {
            extendedStyle |= SetWindowLongFlags.WS_EX_TOOLWINDOW;
        }
        var r = SetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE, extendedStyle);
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
        var windowInfo = WINDOWINFO.Create();
        if (!GetWindowInfo(this.processInfo.MainWindowHandle, ref windowInfo))
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
        else
        {
            this.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        }

        // 本気のフルスクリーンだと何かの拍子に裏側に行ってしまうので、定期的に最前面に持ってくる
        IntPtr hWndHiddenOwner = User32.GetWindow(this.windowHandle, GetWindowCommands.GW_OWNER);
        SetWindowPos(hWndHiddenOwner, new(-1), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);

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

        this.logger.LogDebug($"Window: (x:{left:f2}, y:{top:f2}, w:{width:f2}, h:{height:f2}), マウス位置：({x:f2}, {y:f2} {sw.Elapsed}");
        this.SetCurrentValue(MousePosProperty, new Point(x, y));
        if (this.isEnableCapture && p.showCmd == WindowShowStyle.SW_SHOWMINIMIZED)
        {
            return;
        }
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
