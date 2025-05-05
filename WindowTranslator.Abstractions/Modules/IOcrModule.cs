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
