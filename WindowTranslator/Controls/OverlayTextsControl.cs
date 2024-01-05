using System.Windows;
using System.Windows.Controls;

namespace WindowTranslator.Controls;

public class OverlayTextsControl : Control
{
    static OverlayTextsControl() => DefaultStyleKeyProperty.OverrideMetadata(typeof(OverlayTextsControl), new FrameworkPropertyMetadata(typeof(OverlayTextsControl)));

    public IEnumerable<TextRect> Texts
    {
        get => (IEnumerable<TextRect>)GetValue(TextsProperty);
        set => SetValue(TextsProperty, value);
    }

    /// <summary>Identifies the <see cref="Texts"/> dependency property.</summary>
    public static readonly DependencyProperty TextsProperty = DependencyProperty.Register(nameof(Texts), typeof(IEnumerable<TextRect>), typeof(OverlayTextsControl), new PropertyMetadata(Enumerable.Empty<TextRect>()));

    public double RectWidth
    {
        get => (double)GetValue(RectWidthProperty);
        set => SetValue(RectWidthProperty, value);
    }

    /// <summary>Identifies the <see cref="RectWidth"/> dependency property.</summary>
    public static readonly DependencyProperty RectWidthProperty = DependencyProperty.Register(nameof(RectWidth), typeof(double), typeof(OverlayTextsControl), new PropertyMetadata(double.NaN));

    public double RectHeight
    {
        get => (double)GetValue(RectHeightProperty);
        set => SetValue(RectHeightProperty, value);
    }

    /// <summary>Identifies the <see cref="RectHeight"/> dependency property.</summary>
    public static readonly DependencyProperty RectHeightProperty = DependencyProperty.Register(nameof(RectHeight), typeof(double), typeof(OverlayTextsControl), new PropertyMetadata(double.NaN));

    public Point MousePos
    {
        get => (Point)GetValue(MousePosProperty);
        set => SetValue(MousePosProperty, value);
    }

    /// <summary>Identifies the <see cref="MousePos"/> dependency property.</summary>
    public static readonly DependencyProperty MousePosProperty =
        DependencyProperty.Register(nameof(MousePos), typeof(Point), typeof(OverlayTextsControl), new PropertyMetadata(new Point(double.NaN, double.NaN)));

    public double Scale
    {
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Identifies the <see cref="Scale"/> dependency property.</summary>
    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.Register(nameof(Scale), typeof(double), typeof(OverlayTextsControl), new PropertyMetadata(1.0));

}
