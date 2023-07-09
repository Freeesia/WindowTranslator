namespace WindowTranslator.Modules;

/// <summary>
/// Interface for translation modules.
/// </summary>
public interface ITranslateModule
{
    /// <summary>
    /// 渡されたテキストを翻訳します。
    /// </summary>
    /// <param name="srcTexts">翻訳するテキストの配列。</param>
    /// <returns>翻訳されたテキストの配列。</returns>
    ValueTask<string[]> TranslateAsync(string[] srcTexts);
}