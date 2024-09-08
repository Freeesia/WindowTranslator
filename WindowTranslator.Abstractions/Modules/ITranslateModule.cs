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

    /// <summary>
    /// 翻訳モジュールに用語を登録します。
    /// </summary>
    /// <param name="glossary">用語</param>
    /// <returns>非同期処理</returns>
    ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
        => default;
}