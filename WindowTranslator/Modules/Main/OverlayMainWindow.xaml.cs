using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using PInvoke;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
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
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.windowHandle = new WindowInteropHelper(this).Handle;
        // ShowInTaskbarをfalseにすると↓の方法で一番上に表示する必要がある
        // https://social.msdn.microsoft.com/Forums/en-US/cdbe457f-d653-4a18-9295-bb9b609bc4e3/desktop-apps-on-top-of-metro-extended
        IntPtr hWndHiddenOwner = User32.GetWindow(this.windowHandle, GetWindowCommands.GW_OWNER);
        SetWindowPos(hWndHiddenOwner, new(-1), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        var extendedStyle = (SetWindowLongFlags)GetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE);
        SetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE, extendedStyle | SetWindowLongFlags.WS_EX_TRANSPARENT);
        this.timer.Start();
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

        var borderWidth = SystemParameters.ThickVerticalBorderWidth;
        var borderHeight = SystemParameters.ThickVerticalBorderWidth;

        var left = (clientRect.left - borderWidth) / dpiScaleX;
        var top = clientRect.top / dpiScaleY;
        var width = (clientRect.right - clientRect.left + borderWidth * 2) / dpiScaleX;
        var height = (clientRect.bottom - clientRect.top + borderHeight) / dpiScaleY;

        this.SetCurrentValue(LeftProperty, left);
        this.SetCurrentValue(TopProperty, top);
        this.SetCurrentValue(WidthProperty, width);
        this.SetCurrentValue(HeightProperty, height);
        this.logger.LogDebug($"(x:{left:f2}, y:{top:f2}, w:{width:f2}, h:{height:f2}) {sw.Elapsed}");
    }
}
