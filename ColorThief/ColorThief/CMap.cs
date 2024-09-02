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

    public List<QuantizedColor> GeneratePalette()
        => palette ??= (from vBox in vboxes
                        let rgb = vBox.Avg(false)
                        let color = Color.FromArgb(rgb[0], rgb[1], rgb[2])
                        select new QuantizedColor(color, vBox.Count(false)))
                       .ToList();
}
