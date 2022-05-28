namespace WindowTranslator;

public record TextRect(string Text, double X, double Y, double Width, double Height, double FontSize, int Line)
{
    public TextRect(string text, double x, double y, double width, double height)
        : this(text, x, y, width, height, height, 1)
    {
    }
};