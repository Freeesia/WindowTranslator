namespace WindowTranslator.Modules;

/// <summary>
/// 翻訳前後のテキストに対してフィルター処理を行うモジュールのインターフェース
/// </summary>
public interface IFilterModule
{
    /// <summary>
    /// 優先度
    /// </summary>
    /// <remarks>
    /// 数値が大きい順に翻訳前処理が行われ、小さい順に翻訳後処理が行われる
    /// </remarks>
    double Priority => 0;

    /// <summary>
    /// 翻訳前に行うフィルター処理
    /// </summary>
    /// <param name="texts">処理対象のテキスト</param>
    /// <returns>フィンルター後のテキスト</returns>
    IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts);

    /// <summary>
    /// 翻訳後に行うフィルター処理
    /// </summary>
    /// <param name="texts">処理対象のテキスト</param>
    /// <returns>フィンルター後のテキスト</returns>
    IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts);
}
