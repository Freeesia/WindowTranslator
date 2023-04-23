namespace WindowTranslator.Modules;

/// <summary>
/// キャッシュモジュールのインターフェースです。
/// </summary>
public interface ICacheModule
{
    /// <summary>
    /// テキストがキャッシュに存在するかどうかを返します。
    /// </summary>
    bool Contains(string src);

    /// <summary>
    /// 翻訳結果をキャッシュに追加します。
    /// </summary>
    void AddRange(IEnumerable<(string src, string dst)> pairs);

    /// <summary>
    /// キャッシュから翻訳結果を取得します。
    /// </summary>
    string Get(string src);
}
