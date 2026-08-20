using System.Drawing;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// OCRの観測結果をフレーム間で追跡し、安定化したテキスト領域を返す。
/// </summary>
public interface IOcrTextTracker
{
    /// <summary>
    /// 現在フレームのOCR観測でトラックを更新する。画像サイズが変わった場合は既存のトラックを破棄する。
    /// </summary>
    IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize);

    /// <summary>
    /// 保持しているすべてのトラックを破棄する。
    /// </summary>
    void Reset();
}
