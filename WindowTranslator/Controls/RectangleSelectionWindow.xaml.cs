using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Controls;

/// <summary>
/// 蟇ｾ雎｡繧ｦ繧｣繝ｳ繝峨え縺ｮ繧ｯ繝ｩ繧､繧｢繝ｳ繝磯伜沺縺ｫ驥阪・縺ｦ遏ｩ蠖｢繧帝∈謚槭☆繧九え繧｣繝ｳ繝峨え
/// </summary>
public partial class RectangleSelectionWindow : Window
{
    /// <summary>
    /// 驕ｸ謚槭＆繧後◆遏ｩ蠖｢縺ｨ縺励※謇ｱ縺・怙蟆上・螟ｧ縺阪＆・医け繝ｩ繧､繧｢繝ｳ繝磯伜沺縺ｫ蟇ｾ縺吶ｋ蜑ｲ蜷茨ｼ・
    /// </summary>
    private const double MinimumRelativeSize = 0.005;

    private readonly nint targetHandle;
    private Point startPoint;
    private bool isSelecting;

    /// <summary>
    /// 驕ｸ謚槭＆繧後◆遏ｩ蠖｢・医け繝ｩ繧､繧｢繝ｳ繝磯伜沺縺ｫ蟇ｾ縺吶ｋ逶ｸ蟇ｾ蠎ｧ讓・0.0-1.0・・
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
        if (!TryFitToTargetClientArea())
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
    /// 蟇ｾ雎｡繧ｦ繧｣繝ｳ繝峨え縺ｮ繧ｯ繝ｩ繧､繧｢繝ｳ繝磯伜沺縺ｫ荳閾ｴ縺吶ｋ繧医≧縺ｫ繧ｦ繧｣繝ｳ繝峨え繧帝・鄂ｮ縺吶ｋ
    /// </summary>
    /// <returns>驟咲ｽｮ縺ｧ縺阪◆蝣ｴ蜷医・<see langword="true"/></returns>
    private bool TryFitToTargetClientArea()
    {
        var windowInfo = new WINDOWINFO() { cbSize = (uint)Marshal.SizeOf<WINDOWINFO>() };
        if (this.targetHandle == IntPtr.Zero || !GetWindowInfo(new(this.targetHandle), ref windowInfo))
        {
            return false;
        }

        var client = windowInfo.rcClient;
        var width = client.right - client.left;
        var height = client.bottom - client.top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        // Win32縺ｮ繧ｹ繧ｯ繝ｪ繝ｼ繝ｳ蠎ｧ讓・迚ｩ逅・ヴ繧ｯ繧ｻ繝ｫ)繧淡PF縺ｮ蠎ｧ讓・DIP)縺ｫ螟画鋤縺吶ｋ
        var dpiScale = GetDpiForSystem() / 96.0;
        SetCurrentValue(LeftProperty, client.left / dpiScale);
        SetCurrentValue(TopProperty, client.top / dpiScale);
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

        // 隱､繧ｯ繝ｪ繝・け縺ｫ繧医ｋ讌ｵ蟆上・遏ｩ蠖｢縺ｯ驕ｸ謚槭＠逶ｴ縺励※繧ゅｉ縺・
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
