using System.Drawing;

namespace StudioFreesia.ColorThief;

public record QuantizedColor(Color Color, int Population)
{
    public bool IsDark { get; } = CalculateYiqLuma(Color) < 128;

    private static int CalculateYiqLuma(Color color) => Convert.ToInt32(Math.Round((299 * color.R + 587 * color.G + 114 * color.B) / 1000f));
}