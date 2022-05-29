using System.Drawing;

namespace WindowTranslator;

public record TextRect(string Text, double X, double Y, double Width, double Height, double FontSize, int Line, Color Foreground, Color Background)
{
    public TextRect(string text, double x, double y, double width, double height)
        : this(text, x, y, width, height, height, 1)
    {
    }

    public TextRect(string text, double x, double y, double width, double height, double fontSize, int line)
        : this(text, x, y, width, height, fontSize, line, Color.Red, Color.WhiteSmoke)
    {
    }
};