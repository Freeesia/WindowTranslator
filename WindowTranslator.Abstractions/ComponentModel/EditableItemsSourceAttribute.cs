namespace WindowTranslator.ComponentModel;

/// <summary>
/// プロパティを編集可能なComboBoxとして表示するためのマーカー属性です。
/// 候補一覧はView側の履歴ストアから自動的に提供されます。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class EditableItemsSourceAttribute : Attribute
{
}
