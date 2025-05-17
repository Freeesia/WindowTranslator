using PropertyTools.DataAnnotations;

namespace WindowTranslator.Modules.Cache;
/// <summary>
/// キャッシュモジュールのパラメータを表します。
/// </summary>
public class CacheParam : IPluginParam
{
    /// <summary>
    /// 同じテキストと判定するための閾値
    /// </summary>
    [Slidable(0, 1, .01, .1, true, .01)]
    [FormatString("P2")]
    public double FuzzyMatchThreshold { get; set; } = 0.8;
}