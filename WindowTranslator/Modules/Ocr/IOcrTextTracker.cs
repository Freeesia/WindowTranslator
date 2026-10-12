using System.Drawing;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// OCRの観測結果をフレーム間で追跡し、安定化したテキスト領域を返す。
/// </summary>
public interface IOcrTextTracker
{
    /// <summary>
    /// 最新のOCR観測でトラックを更新し、安定化したテキスト領域を返す。
    /// </summary>
    /// <param name="timestamp">記録の再入力時の処理時刻。省略時は内部の時計を使用する。</param>
    IReadOnlyList<TextRect> Update(IEnumerable<TextRect> observations, Size imageSize, TimeSpan? timestamp = null);

    /// <summary>
    /// 保持しているすべてのトラックを破棄する。
    /// </summary>
    void Reset();
}
