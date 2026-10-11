using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WindowTranslator.TraceViewer;

public sealed class OcrDrawingSurface : FrameworkElement
{
    private int imageWidth;
    private int imageHeight;
    private IReadOnlyList<TextRect> rawRectangles = [];
    private IReadOnlyList<TextRect> trackedRectangles = [];
    private static readonly Typeface typeface = new("Yu Gothic UI");

    public OcrDrawingSurface()
    {
        ClipToBounds = true;
    }

    internal void SetFrame(int width, int height, IReadOnlyList<TextRect> raw, IReadOnlyList<TextRect> tracked)
    {
        imageWidth = width;
        imageHeight = height;
        rawRectangles = raw;
        trackedRectangles = tracked;
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
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        DrawRectangles(drawingContext, rawRectangles, Brushes.DeepSkyBlue, scale, pixelsPerDip);
        DrawRectangles(drawingContext, trackedRectangles, Brushes.Orange, scale, pixelsPerDip);

        drawingContext.Pop();
        drawingContext.Pop();
        drawingContext.Pop();
    }

    private static void DrawRectangles(DrawingContext drawingContext, IReadOnlyList<TextRect> rectangles,
        Brush color, double scale, double pixelsPerDip)
    {
        Pen outline = new(color, 1 / scale);
        foreach (TextRect rect in rectangles)
        {
            drawingContext.PushTransform(new RotateTransform(rect.Angle, rect.X, rect.Y));
            drawingContext.DrawRectangle(null, outline, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
            FormattedText text = new(rect.SourceText, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, rect.FontSize, color, pixelsPerDip);
            if (rect.MultiLine)
            {
                text.MaxTextWidth = rect.Width;
            }
            // 高さ制限で行が消えないよう、文字列全体を矩形内に縮小して描画する。
            double textScale = Math.Min(1, Math.Min(
                rect.Width / text.WidthIncludingTrailingWhitespace, rect.Height / text.Height));
            drawingContext.PushTransform(new ScaleTransform(textScale, textScale, rect.X, rect.Y));
            drawingContext.DrawText(text, new Point(rect.X, rect.Y));
            drawingContext.Pop();
            drawingContext.Pop();
        }
    }
}
