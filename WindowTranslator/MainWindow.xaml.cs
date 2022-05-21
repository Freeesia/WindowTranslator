using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WindowTranslator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.DataContext = new MainViewModel();
    }
}


public class BorderAdorner : Adorner
{
    public BorderAdorner(UIElement ui) : base(ui) { }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = new Rect(this.AdornedElement.DesiredSize);

        drawingContext.DrawRectangle(
            null,
            new Pen(Brushes.Red, 5),
            rect
        );

    }
}
