using PropertyTools.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WindowTranslator.Modules.Settings;
internal class SettingsPropertyGridFactory : PropertyGridControlFactory
{
    public override FrameworkElement CreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        if (property.Is(typeof(ICommand)))
        {
            var button = new Button();
            button.SetBinding(System.Windows.Controls.Primitives.ButtonBase.CommandProperty, property.CreateOneWayBinding());
            button.SetCurrentValue(ContentControl.ContentProperty, property.DisplayName);
            button.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            return button;
        }
        return base.CreateControl(property, options);
    }

    protected override FrameworkElement CreateBoolControl(PropertyItem property)
    {
        var fe = base.CreateBoolControl(property);
        fe.SetCurrentValue(ContentControl.ContentProperty, property.DisplayName);
        return fe;
    }
}
