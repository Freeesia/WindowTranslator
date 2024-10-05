
using System.Drawing;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Modules.Ocr;

public class WindowsMediaOcrParam : IPluginParam
{
    [Category("MergeThrethold")]
    [FormatString("P2")]
    [Slidable(0, 0.2, .001, .01, true, .001)]
    public double PosThrethold { get; set; } = .005;

    [Category("MergeThrethold")]
    [Slidable(0, 1, .01, .1, true, .01)]
    [FormatString("P2")]
    public double LeadingThrethold { get; set; } = .80;

    [Category("MergeThrethold")]
    [Slidable(0, 3, .01, .1, true, .01)]
    [FormatString("P2")]
    public double SpacingThreshold { get; set; } = 1.1;

    [Category("MergeThrethold")]
    [Slidable(0, 1, .01, .1, true, .01)]
    [FormatString("P2")]
    public double FontSizeThrethold { get; set; } = .25;

    [Category("ColorThrethold")]
    public bool IsOnlyTargetColor { get; set; }

    [Category("ColorThrethold")]
    [PropertyView(nameof(TargetColorsControl))]
    public List<Color> TargetColors { get; set; } = [];
}
