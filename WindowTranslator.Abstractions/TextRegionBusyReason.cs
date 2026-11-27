namespace WindowTranslator;

/// <summary>
/// テキスト領域が表示可能な翻訳結果を待っている理由。
/// </summary>
[Flags]
public enum TextRegionBusyReason
{
    /// <summary>
    /// 待機していない。
    /// </summary>
    None = 0,

    /// <summary>
    /// 翻訳結果を待っている。
    /// </summary>
    Translation = 1 << 0,

    /// <summary>
    /// 文字送りの完了を待っている。
    /// </summary>
    Typewriter = 1 << 1,
}
