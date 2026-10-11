using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WindowTranslator.TraceViewer;

public sealed class OcrDrawingSurface : FrameworkElement
{
    private int imageWidth;
    private int imageHeight;
    private IReadOnlyList<TextRect> rectangles = [];
    private static readonly Typeface typeface = new("Yu Gothic UI");

    public OcrDrawingSurface()
    {
        ClipToBounds = true;
    }

    internal void SetFrame(int width, int height, IReadOnlyList<TextRect> rects)
    {
        imageWidth = width;
        imageHeight = height;
        rectangles = rects;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Brushes.Black, null, new Rect(RenderSize));
        if (imageWidth == 0 || imageHeight == 0 || ActualWidth == 0 || ActualHeight == 0)
        {
            return;
        }

        double scale = Math.Min(ActualWidth / imageWidth, ActualHeight / imageHeight);
        drawingContext.PushTransform(new TranslateTransform(
            (ActualWidth - imageWidth * scale) / 2, (ActualHeight - imageHeight * scale) / 2));
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, imageWidth, imageHeight)));
        Pen outline = new(Brushes.DeepSkyBlue, 1 / scale);
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (TextRect rect in rectangles)
        {
            drawingContext.PushTransform(new RotateTransform(rect.Angle, rect.X, rect.Y));
            drawingContext.DrawRectangle(null, outline, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
            FormattedText text = new(rect.SourceText, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, rect.FontSize, Brushes.White, pixelsPerDip)
            {
                MaxTextWidth = rect.Width,
                MaxTextHeight = rect.Height,
                Trimming = TextTrimming.CharacterEllipsis,
            };
            if (!rect.MultiLine)
            {
                text.MaxLineCount = 1;
            }
            drawingContext.DrawText(text, new Point(rect.X, rect.Y));
            drawingContext.Pop();
        }

        drawingContext.Pop();
        drawingContext.Pop();
        drawingContext.Pop();
    }
}
