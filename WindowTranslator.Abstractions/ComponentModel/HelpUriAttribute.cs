namespace WindowTranslator.ComponentModel;

/// <summary>
/// プロパティのヘルプドキュメントURIを指定する属性です。
/// </summary>
/// <param name="uri">ヘルプドキュメントのURI</param>
[AttributeUsage(AttributeTargets.Property)]
public class HelpUriAttribute(string uri) : Attribute
{
    /// <summary>
    /// ヘルプドキュメントのURIを取得します。
    /// </summary>
    public string Uri { get; } = uri;
}
