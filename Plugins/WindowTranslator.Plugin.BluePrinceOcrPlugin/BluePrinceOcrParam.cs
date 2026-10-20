using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using CategoryAttribute = System.ComponentModel.CategoryAttribute;
using PropertyTools.DataAnnotations;

namespace WindowTranslator.Plugin.BluePrinceOcrPlugin;

[DisplayName("BluPrinceOcr")]
public sealed class BluePrinceOcrParam : IPluginParam
{
    public const double DefaultLineSpacingTolerance = 1.45;

    [Category("BluPrinceOcr")]
    [DisplayName("段落内の行送り許容倍率")]
    [Description("推定した通常の行送りに対する許容倍率。小さくすると段落を分けやすく、大きくすると行を結合しやすくなります。")]
    [Slidable(1.05, 2.5, .05, .1, true, .05)]
    [FormatString("F2")]
    public double LineSpacingTolerance { get; set; } = DefaultLineSpacingTolerance;

    internal static double Normalize(double value)
        => double.IsFinite(value) ? Math.Clamp(value, 1.05, 2.5) : DefaultLineSpacingTolerance;
}

