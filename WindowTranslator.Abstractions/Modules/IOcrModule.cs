using PropertyTools.DataAnnotations;
using CategoryAttribute = System.ComponentModel.CategoryAttribute;

namespace WindowTranslator.Modules;

/// <summary>
/// テキスト認識モジュールのインターフェース
/// </summary>
public interface IOcrModule
{
#if WINDOWS
    /// <summary>
    /// 画像からテキストを認識する
    /// </summary>
    ValueTask<IEnumerable<TextRect>> RecognizeAsync(Windows.Graphics.Imaging.SoftwareBitmap bitmap);
#endif
}

/// <summary>
/// 基本的なOCRパラメータ
/// </summary>
public class BasicOcrParam : IPluginParam
{
    /// <summary>
    /// 認識スケール
    /// </summary>
    [Category("Recognize")]
    [Slidable(0.5, 4, 0.1, 0.5, true, 0.1)]
    [FormatString("F2")]
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// X位置のしきい値
    /// </summary>
    [Category("MergeThrethold")]
    [FormatString("P2")]
    [Slidable(0, 0.2, .001, .01, true, .001)]
    public double XPosThrethold { get; set; } = .005;

    /// <summary>
    /// Y位置のしきい値
    /// </summary>
    [Category("MergeThrethold")]
    [FormatString("P2")]
    [Slidable(0, 0.2, .001, .01, true, .001)]
    public double YPosThrethold { get; set; } = .005;

    /// <summary>
    /// 行間のしきい値
    /// </summary>
    [Category("MergeThrethold")]
    [Slidable(0, 1, .01, .1, true, .01)]
    [FormatString("P2")]
    public double LeadingThrethold { get; set; } = .80;

    /// <summary>
    /// 文字間のしきい値
    /// </summary>
    [Category("MergeThrethold")]
    [Slidable(0, 3, .01, .1, true, .01)]
    [FormatString("P2")]
    public double SpacingThreshold { get; set; } = 1.1;

    /// <summary>
    /// フォントサイズのしきい値
    /// </summary>
    [Category("MergeThrethold")]
    [Slidable(0, 1, .01, .1, true, .01)]
    [FormatString("P2")]
    public double FontSizeThrethold { get; set; } = .25;

    /// <summary>
    /// リストのマージを避けるかどうか
    /// </summary>
    [Category("MergeThrethold")]
    public bool IsAvoidMergeList { get; set; } = false;

    /// <summary>
    /// バッファサイズ
    /// </summary>
    [Category("Misc")]
    [Spinnable]
    public int BufferSize { get; set; } = 3;
}
