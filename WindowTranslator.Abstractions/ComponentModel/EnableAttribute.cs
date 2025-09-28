namespace WindowTranslator.ComponentModel;

/// <summary>
/// プロパティが有効かどうかを示す属性です。
/// </summary>
/// <param name="isEnable">有効かどうか</param>
[AttributeUsage(AttributeTargets.Property)]
public class EnableAttribute(bool isEnable) : Attribute
{
    /// <summary>
    /// プロパティが有効かどうかを示す値を取得します。
    /// </summary>
    public bool IsEnable { get; } = isEnable;
}
