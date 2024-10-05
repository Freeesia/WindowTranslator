namespace WindowTranslator;

/// <summary>
/// プロパティの見た目に利用するビューを指定するプラグイン
/// </summary>
public interface IPropertyView
{
    /// <summary>
    /// ビューの名前
    /// </summary>
    string ViewName { get; }

    /// <summary>
    /// ビューの型
    /// </summary>
    Type ViewType { get; }
}
