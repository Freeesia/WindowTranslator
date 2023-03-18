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

    public static readonly DependencyProperty TextsProperty = DependencyProperty.Register(nameof(Texts), typeof(IEnumerable<TextRect>), typeof(OverlayTextsControl), new PropertyMetadata(Enumerable.Empty<TextRect>()));

    public double RectWidth
    {
        get => (double)GetValue(RectWidthProperty);
        set => SetValue(RectWidthProperty, value);
    }

    public static readonly DependencyProperty RectWidthProperty = DependencyProperty.Register(nameof(RectWidth), typeof(double), typeof(OverlayTextsControl), new PropertyMetadata(double.NaN));

    public double RectHeight
    {
        get => (double)GetValue(RectHeightProperty);
        set => SetValue(RectHeightProperty, value);
    }

    public static readonly DependencyProperty RectHeightProperty = DependencyProperty.Register(nameof(RectHeight), typeof(double), typeof(OverlayTextsControl), new PropertyMetadata(double.NaN));

}
