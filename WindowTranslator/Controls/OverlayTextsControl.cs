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

    public bool IsSwapVisibility
    {
        get => (bool)GetValue(IsSwapVisibilityProperty);
        set => SetValue(IsSwapVisibilityProperty, value);
    }

    /// <summary>Identifies the <see cref="IsSwapVisibility"/> dependency property.</summary>
    public static readonly DependencyProperty IsSwapVisibilityProperty =
        DependencyProperty.Register(nameof(IsSwapVisibility), typeof(bool), typeof(OverlayTextsControl), new PropertyMetadata(false));

    public double Scale
    {
        get => (double)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Identifies the <see cref="Scale"/> dependency property.</summary>
    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.Register(nameof(Scale), typeof(double), typeof(OverlayTextsControl), new PropertyMetadata(1.0));

    /// <summary>Identifies the attached <see cref="ZOrder"/> dependency property.</summary>
    public static readonly DependencyProperty ZOrderProperty =
        DependencyProperty.RegisterAttached("ZOrder", typeof(int), typeof(OverlayTextsControl), new PropertyMetadata(0));

    /// <summary>Gets the ZOrder value for the specified element.</summary>
    /// <param name="element">The element from which to read the property value.</param>
    /// <returns>The ZOrder value of the element.</returns>
    public static int GetZOrder(DependencyObject element) => (int)element.GetValue(ZOrderProperty);

    /// <summary>Sets the ZOrder value for the specified element.</summary>
    /// <param name="element">The element to which to write the attached property.</param>
    /// <param name="value">The ZOrder value to set.</param>
    public static void SetZOrder(DependencyObject element, int value) => element.SetValue(ZOrderProperty, value);

}
