using Windows.Graphics.Imaging;

namespace WindowTranslator.Modules;

/// <summary>
/// テキスト認識モジュールのインターフェース
/// </summary>
public interface IOcrModule
{
    /// <summary>
    /// 画像からテキストを認識する
    /// </summary>
    ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap);
}
