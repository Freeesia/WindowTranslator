using PropertyTools.DataAnnotations;

namespace WindowTranslator.Modules.Ocr;

public class WindowsMediaOcrParam : IPluginParam
{
    [Category("Recognize")]
    [Slidable(0.5, 4, 0.1, 0.5, true, 0.1)]
    [FormatString("F2")]
    public double Scale { get; set; } = 1.0;

    [Category("MergeThrethold")]
    [FormatString("P2")]
    [Slidable(0, 0.2, .001, .01, true, .001)]
    public double XPosThrethold { get; set; } = .005;

    [Category("MergeThrethold")]
    [FormatString("P2")]
    [Slidable(0, 0.2, .001, .01, true, .001)]
    public double YPosThrethold { get; set; } = .005;

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

    [Category("MergeThrethold")]
    public bool IsAvoidMergeList { get; set; } = false;
}
