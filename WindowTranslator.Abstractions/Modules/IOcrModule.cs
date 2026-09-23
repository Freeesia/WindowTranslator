using PropertyTools.DataAnnotations;
using CategoryAttribute = System.ComponentModel.CategoryAttribute;

namespace WindowTranslator.Modules;

/// <summary>
/// テキスト認識モジュールのインターフェース
/// </summary>
public interface IOcrModule
{
    /// <summary>
    /// モジュール名
    /// </summary>
    public string Name => GetType().Name;

#if WINDOWS
    /// <summary>
    /// 1回のキャプチャーに含まれるOCR対象画像からテキストを認識する
    /// </summary>
    ValueTask<IReadOnlyList<TextRect>> RecognizeAsync(OcrCaptureInput input);
#endif
}

#if WINDOWS
/// <summary>
/// 1回のキャプチャーに対するOCR入力
/// </summary>
/// <param name="Source">全体のコンテキストを把握するための元画像</param>
/// <param name="Regions">実際にOCRする範囲の一覧</param>
public sealed record OcrCaptureInput(
    Windows.Graphics.Imaging.SoftwareBitmap Source,
    IReadOnlyList<OcrRegionInput> Regions);

/// <summary>
/// 1つのOCR対象範囲
/// </summary>
/// <param name="Bounds">全体画像上の切り出し範囲</param>
/// <param name="Keyword">翻訳コンテキストに設定するキーワード。未指定の場合は認識結果のコンテキストを維持する</param>
public sealed record OcrRegionInput(RectInfo Bounds, string? Keyword = null);
#endif

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
    /// 明るさ（-127 - 128）
    /// </summary>
    [Category("Recognize")]
    [Slidable(-127, 128, 1, 10, true, 1)]
    public int Brightness { get; set; } = 0;

    /// <summary>
    /// コントラスト（-99 - 100）
    /// </summary>
    [Category("Recognize")]
    [Slidable(-99, 100, 1, 10, true, 1)]
    public int Contrast { get; set; } = 0;

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
    /// OCR対象範囲のリスト
    /// </summary>
    /// <remarks>
    /// 1件以上設定されている場合は、画像全体ではなく指定範囲内だけをOCRする。
    /// 範囲が重なる場合は、リストの順序が優先度を表す（前方が高優先度）。
    /// </remarks>
    [Category("PriorityRect")]
    public List<PriorityRect> PriorityRects { get; set; } = [];
}
