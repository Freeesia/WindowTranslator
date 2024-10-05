namespace WindowTranslator.ComponentModel;

/// <summary>
/// プロパティの見た目に利用するビューを指定する属性
/// </summary>
/// <param name="viewName">ビュー名</param>
[AttributeUsage(AttributeTargets.Property)]
public class PropertyViewAttribute(string viewName) : Attribute
{
    /// <summary>
    /// パラメータの見た目に利用するビュー名を指定します。
    /// </summary>
    public string ViewName { get; } = viewName;
}
