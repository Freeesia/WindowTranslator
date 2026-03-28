namespace WindowTranslator.ComponentModel;

/// <summary>
/// プロパティを編集可能なComboBoxとして表示し、候補一覧をバインドするプロパティ名を指定する属性です。
/// </summary>
/// <param name="itemsSourcePropertyName">候補一覧を提供するプロパティの名前</param>
[AttributeUsage(AttributeTargets.Property)]
public class EditableItemsSourceAttribute(string itemsSourcePropertyName) : Attribute
{
    /// <summary>
    /// 候補一覧を提供するプロパティの名前を取得します。
    /// </summary>
    public string ItemsSourcePropertyName { get; } = itemsSourcePropertyName;
}
