namespace WindowTranslator.ComponentModel;

/// <summary>
/// プロパティのヘルプドキュメントページ名を指定する属性です。
/// </summary>
/// <param name="pageName">ヘルプドキュメントのページ名（例: "OcrModule"）</param>
[AttributeUsage(AttributeTargets.Property)]
public class HelpUriAttribute(string pageName) : Attribute
{
    /// <summary>
    /// ヘルプドキュメントのページ名を取得します。
    /// </summary>
    public string PageName { get; } = pageName;
}
