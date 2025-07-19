using System.Drawing;

namespace WindowTranslator.Modules;

/// <summary>
/// 色変換モジュールのインターフェース
/// </summary>
public interface IColorModule
{
#if WINDOWS
    /// <summary>
    /// 画像とテキスト矩形から色を変換する
    /// </summary>
    ValueTask<IEnumerable<TextRect>> ConvertColorAsync(Windows.Graphics.Imaging.SoftwareBitmap bitmap, IEnumerable<TextRect> texts);
#endif
}
