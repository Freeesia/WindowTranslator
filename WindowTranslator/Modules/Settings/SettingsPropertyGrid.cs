using PropertyTools.Wpf;
using System.ComponentModel;
using System.Windows;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Modules.Settings;

/// <summary>
/// 履歴付き編集可能ComboBoxをサポートするPropertyGridコントロールです。
/// ダイアログが閉じる際に<see cref="SettingsPropertyGridOperator.HistoryStore"/>へ現在値を保存します。
/// </summary>
internal class SettingsPropertyGrid : PropertyGrid
{
    private static readonly Dictionary<Type, PropertyDescriptorCollection> propertyCache = [];

    public SettingsPropertyGrid()
    {
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (this.Operator is not SettingsPropertyGridOperator { HistoryStore: { } store }) return;
        if (this.SelectedObject is not { } obj) return;

        var saved = false;
        foreach (var param in GetPluginParams(obj))
        {
            var paramType = param.GetType();
            if (!propertyCache.TryGetValue(paramType, out var properties))
            {
                properties = TypeDescriptor.GetProperties(param);
                propertyCache[paramType] = properties;
            }
            foreach (PropertyDescriptor pd in properties)
            {
                var attr = pd.Attributes.OfType<EditableItemsSourceAttribute>().FirstOrDefault();
                if (attr != null && pd.GetValue(param) is string value && !string.IsNullOrWhiteSpace(value))
                {
                    store.AddHistory($"{paramType.Name}.{pd.Name}", value);
                    saved = true;
                }
            }
        }

        if (saved)
        {
            store.Save();
        }
    }

    private static IEnumerable<IPluginParam> GetPluginParams(object obj)
    {
        foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(obj))
        {
            if (typeof(IReadOnlyList<IPluginParam>).IsAssignableFrom(pd.PropertyType)
                && pd.GetValue(obj) is IReadOnlyList<IPluginParam> list)
            {
                foreach (var param in list)
                {
                    yield return param;
                }
            }
        }
    }
}
