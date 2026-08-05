using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Controls;

/// <summary>
/// 対象ウィンドウのクライアント領域に重ねて矩形を選択するウィンドウ
/// </summary>
public partial class RectangleSelectionWindow : Window
{
    /// <summary>
    /// 選択された矩形として扱う最小の大きさ（クライアント領域に対する割合）
    /// </summary>
    private const double MinimumRelativeSize = 0.005;

    private readonly nint targetHandle;
    private Point startPoint;
    private bool isSelecting;

    /// <summary>
    /// 選択された矩形（クライアント領域に対する相対座標 0.0-1.0）
    /// </summary>
    public PriorityRect? SelectedRect { get; private set; }

    public RectangleSelectionWindow(nint targetHandle)
    {
        this.targetHandle = targetHandle;
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (!TryFitToCaptureArea())
        {
            DialogResult = false;
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// キャプチャ画像と同じ範囲になるようにウィンドウを配置する
    /// </summary>
    /// <remarks>
    /// キャプチャ画像はウィンドウ全体のフレームから
    /// <see cref="WINDOWINFO.rcWindow"/>の上端と<see cref="WINDOWINFO.rcClient"/>の左右下端で切り出した範囲になるため、
    /// <see cref="Main.OverlayMainWindow"/>と同じ計算で位置と大きさを求める
    /// </remarks>
    /// <returns>配置できた場合は<see langword="true"/></returns>
    private bool TryFitToCaptureArea()
    {
        var windowInfo = new WINDOWINFO() { cbSize = (uint)Marshal.SizeOf<WINDOWINFO>() };
        if (this.targetHandle == IntPtr.Zero || !GetWindowInfo(new(this.targetHandle), ref windowInfo))
        {
            return false;
        }

        var client = windowInfo.rcClient;
        var left = client.left;
        var top = windowInfo.rcWindow.top;
        var placement = default(WINDOWPLACEMENT);
        // 最大化時はウィンドウの上端が画面外に出るため、作業領域の上端を使う
        if (GetWindowPlacement(new(this.targetHandle), ref placement) && placement.showCmd.HasFlag(SHOW_WINDOW_CMD.SW_MAXIMIZE))
        {
            var monitor = MonitorFromWindow(new(this.targetHandle), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = default(MONITORINFOEXW);
            monitorInfo.monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>();
            if (GetMonitorInfo(monitor, ref monitorInfo.monitorInfo))
            {
                top = monitorInfo.monitorInfo.rcWork.top;
            }
        }

        var width = client.right - left;
        var height = client.bottom - top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        // Win32のスクリーン座標(物理ピクセル)をWPFの座標(DIP)に変換する
        var dpiScale = GetDpiForSystem() / 96.0;
        SetCurrentValue(LeftProperty, left / dpiScale);
        SetCurrentValue(TopProperty, top / dpiScale);
        SetCurrentValue(WidthProperty, width / dpiScale);
        SetCurrentValue(HeightProperty, height / dpiScale);
        return true;
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        this.startPoint = e.GetPosition(this.SelectionCanvas);
        this.isSelecting = true;
        this.SelectionCanvas.CaptureMouse();
        this.SelectionRect.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        UpdateSelectionRect(this.startPoint);
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!this.isSelecting)
        {
            return;
        }

        UpdateSelectionRect(e.GetPosition(this.SelectionCanvas));
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!this.isSelecting)
        {
            return;
        }

        this.isSelecting = false;
        this.SelectionCanvas.ReleaseMouseCapture();

        var canvasWidth = this.SelectionCanvas.ActualWidth;
        var canvasHeight = this.SelectionCanvas.ActualHeight;
        if (canvasWidth <= 0 || canvasHeight <= 0)
        {
            return;
        }

        var rect = PriorityRect.FromAbsoluteRect(
            Canvas.GetLeft(this.SelectionRect),
            Canvas.GetTop(this.SelectionRect),
            this.SelectionRect.Width,
            this.SelectionRect.Height,
            (int)canvasWidth,
            (int)canvasHeight);

        // 誤クリックによる極端に小さい矩形は選択し直してもらう
        if (rect.Width < MinimumRelativeSize || rect.Height < MinimumRelativeSize)
        {
            this.SelectionRect.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            this.InfoText.SetCurrentValue(TextBlock.TextProperty, PriorityRectResources.TooSmall);
            return;
        }

        this.SelectedRect = rect;
        DialogResult = true;
        Close();
    }

    private void UpdateSelectionRect(Point currentPoint)
    {
        var x = Math.Clamp(Math.Min(this.startPoint.X, currentPoint.X), 0, this.SelectionCanvas.ActualWidth);
        var y = Math.Clamp(Math.Min(this.startPoint.Y, currentPoint.Y), 0, this.SelectionCanvas.ActualHeight);
        var width = Math.Clamp(Math.Max(this.startPoint.X, currentPoint.X), 0, this.SelectionCanvas.ActualWidth) - x;
        var height = Math.Clamp(Math.Max(this.startPoint.Y, currentPoint.Y), 0, this.SelectionCanvas.ActualHeight) - y;

        Canvas.SetLeft(this.SelectionRect, x);
        Canvas.SetTop(this.SelectionRect, y);
        this.SelectionRect.SetCurrentValue(WidthProperty, width);
        this.SelectionRect.SetCurrentValue(HeightProperty, height);
        this.InfoText.SetCurrentValue(TextBlock.TextProperty, $"{PriorityRectResources.Selecting}: ({x:F0}, {y:F0}) - ({width:F0} x {height:F0})");
    }
}
