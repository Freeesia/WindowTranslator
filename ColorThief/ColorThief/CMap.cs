using System.Drawing;

namespace StudioFreesia.ColorThief;

/// <summary>
///     Color map
/// </summary>
internal class CMap
{
    private readonly List<VBox> vboxes = [];
    private List<QuantizedColor>? palette;

    public void Push(VBox box)
    {
        palette = null;
        vboxes.Add(box);
    }

    public IEnumerable<QuantizedColor> GeneratePalette()
        => palette ??= vboxes.Select(vBox =>
        {
            var rgb = vBox.Avg(false);
            return new QuantizedColor(Color.FromArgb(rgb[0], rgb[1], rgb[2]), vBox.Count(false));
        }).ToList();
}
