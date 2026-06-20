using PropertyTools.Wpf;
using System.ComponentModel;
using System.Windows;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Modules.Settings;

internal class SettingsPropertyGrid : PropertyGrid
{
    public SettingsPropertyGrid()
    {
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (this.Operator is not SettingsPropertyGridOperator { HistoryStore: { } store }) return;
        if (this.SelectedObject is not { } obj) return;

        var saved = false;
        foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(obj))
        {
            if (!typeof(IReadOnlyList<IPluginParam>).IsAssignableFrom(pd.PropertyType)
                || pd.GetValue(obj) is not IReadOnlyList<IPluginParam> list)
            {
                continue;
            }

            foreach (var param in list)
            {
                foreach (PropertyDescriptor paramPd in TypeDescriptor.GetProperties(param))
                {
                    if (paramPd.Attributes.OfType<EditableItemsSourceAttribute>().Any()
                        && paramPd.GetValue(param) is string value
                        && !string.IsNullOrWhiteSpace(value))
                    {
                        store.AddHistory($"{param.GetType().Name}.{paramPd.Name}", value);
                        saved = true;
                    }
                }
            }
        }

        if (saved)
        {
            store.Save();
        }
    }
}
