using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// 矩形選択ウィンドウ
/// </summary>
public partial class RectangleSelectionWindow : Window
{
    private Point startPoint;
    private bool isSelecting;

    /// <summary>
    /// 選択された矩形（相対座標 0.0-1.0）
    /// </summary>
    public PriorityRect? SelectedRect { get; private set; }

    public RectangleSelectionWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        this.startPoint = e.GetPosition(this.SelectionCanvas);
        this.isSelecting = true;
        this.SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(this.SelectionRect, this.startPoint.X);
        Canvas.SetTop(this.SelectionRect, this.startPoint.Y);
        this.SelectionRect.Width = 0;
        this.SelectionRect.Height = 0;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!this.isSelecting)
        {
            return;
        }

        var currentPoint = e.GetPosition(this.SelectionCanvas);
        var x = Math.Min(this.startPoint.X, currentPoint.X);
        var y = Math.Min(this.startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - this.startPoint.X);
        var height = Math.Abs(currentPoint.Y - this.startPoint.Y);

        Canvas.SetLeft(this.SelectionRect, x);
        Canvas.SetTop(this.SelectionRect, y);
        this.SelectionRect.Width = width;
        this.SelectionRect.Height = height;

        this.InfoText.Text = $"選択中: ({x:F0}, {y:F0}) - ({width:F0} x {height:F0})";
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!this.isSelecting)
        {
            return;
        }

        this.isSelecting = false;

        var x = Canvas.GetLeft(this.SelectionRect);
        var y = Canvas.GetTop(this.SelectionRect);
        var width = this.SelectionRect.Width;
        var height = this.SelectionRect.Height;

        // 最小サイズチェック
        if (width < 10 || height < 10)
        {
            MessageBox.Show("矩形が小さすぎます。もう一度選択してください。", "矩形選択", MessageBoxButton.OK, MessageBoxImage.Warning);
            this.SelectionRect.Visibility = Visibility.Collapsed;
            this.InfoText.Text = "矩形を選択してください（Escキーでキャンセル）";
            return;
        }

        // 相対座標に変換
        var canvasWidth = this.SelectionCanvas.ActualWidth;
        var canvasHeight = this.SelectionCanvas.ActualHeight;

        this.SelectedRect = new PriorityRect(
            x / canvasWidth,
            y / canvasHeight,
            width / canvasWidth,
            height / canvasHeight
        );

        DialogResult = true;
        Close();
    }
}
