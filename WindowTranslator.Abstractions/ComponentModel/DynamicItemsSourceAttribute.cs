namespace WindowTranslator.ComponentModel;

/// <summary>外部から取得した候補を、保存値と表示名を分けた ComboBox に表示します。</summary>
/// <remarks>設定オブジェクトは <see cref="IDynamicItemsSource"/> を実装します。</remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DynamicItemsSourceAttribute : Attribute;

/// <summary>動的な選択候補。</summary>
/// <param name="Value">設定に保存する値。</param>
/// <param name="DisplayName">画面に表示する名前。</param>
public sealed record DynamicItem(object Value, string DisplayName);

/// <summary>プロパティごとの選択候補を提供します。</summary>
public interface IDynamicItemsSource
{
    /// <summary>候補を取得します。画面表示時および候補を開くときに呼び出されます。</summary>
    /// <param name="propertyName">候補の対象プロパティ。</param>
    /// <param name="cancellationToken">画面を閉じたときのキャンセル。</param>
    /// <returns>候補一覧。</returns>
    ValueTask<IReadOnlyList<DynamicItem>> GetItemsAsync(string propertyName, CancellationToken cancellationToken);
}
