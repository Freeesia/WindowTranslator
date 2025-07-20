using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace WindowTranslator.Behaviors;

public sealed class FontSizeToOffsetYBehavior : Behavior<TextBlock>
{
    protected override void OnAttached()
    {
        this.AssociatedObject.Loaded -= OnTextBlockLoaded;
        this.AssociatedObject.Loaded += OnTextBlockLoaded;
    }

    protected override void OnDetaching()
    {
        this.AssociatedObject.Loaded -= OnTextBlockLoaded;
    }

    private void OnTextBlockLoaded(object sender, RoutedEventArgs e)
    {
        this.AssociatedObject.Loaded -= OnTextBlockLoaded;
        var typeface = new Typeface(this.AssociatedObject.FontFamily, this.AssociatedObject.FontStyle, this.AssociatedObject.FontWeight, this.AssociatedObject.FontStretch);
        if (!typeface.TryGetGlyphTypeface(out var glyph))
        {
            return;
        }
        var offsetY = -(glyph.Height - glyph.Baseline) * this.AssociatedObject.FontSize * 0.9;
        this.AssociatedObject.SetCurrentValue(FrameworkElement.MarginProperty, new Thickness(0, offsetY, 0, 0));
    }
}
