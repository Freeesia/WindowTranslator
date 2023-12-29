using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using PInvoke;
using WindowTranslator.Stores;
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

    public OverlayMainWindow(IProcessInfoStore processInfo, IPresentationService presentationService, ILogger<OverlayMainWindow> logger)
    {
        InitializeComponent();
        this.processInfo = processInfo;
        this.presentationService = presentationService;
        this.logger = logger;
        this.timer.Interval = TimeSpan.FromMilliseconds(10);
        this.timer.Tick += (s, e) => UpdateWindowPositionAndSize();
#if DEBUG
        this.Background = System.Windows.Media.Brushes.Red;
        this.Opacity = 0.2;
#endif
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.windowHandle = new WindowInteropHelper(this).Handle;
        var extendedStyle = (SetWindowLongFlags)GetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE);
        var r = SetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE, extendedStyle | SetWindowLongFlags.WS_EX_TRANSPARENT);
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
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        this.timer.Stop();
    }

    private void UpdateWindowPositionAndSize()
    {
        var sw = Stopwatch.StartNew();
        var windowInfo = WINDOWINFO.Create();
        if (!GetWindowInfo(this.processInfo.MainWindowHangle, ref windowInfo))
        {
            this.timer.Stop();
            this.presentationService.CloseWindowAsync(this).Forget();
            return;
        }

        var monitorHandle = MonitorFromWindow(this.processInfo.MainWindowHangle, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        SHCore.GetDpiForMonitor(monitorHandle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY);
        var dpiScaleX = dpiX / 96.0;
        var dpiScaleY = dpiY / 96.0;

        var clientRect = windowInfo.rcClient;
        var windowRect = windowInfo.rcWindow;

        var left = clientRect.left / dpiScaleX;
        var top = windowRect.top / dpiScaleY;
        var width = (clientRect.right - clientRect.left) / dpiScaleX;
        var height = (clientRect.bottom - windowRect.top) / dpiScaleY;

        this.SetCurrentValue(LeftProperty, left);
        this.SetCurrentValue(TopProperty, top);
        this.SetCurrentValue(WidthProperty, width);
        this.SetCurrentValue(HeightProperty, height);
        this.logger.LogDebug($"(x:{left:f2}, y:{top:f2}, w:{width:f2}, h:{height:f2}) {sw.Elapsed}");
    }
}
