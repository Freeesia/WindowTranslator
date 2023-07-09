using PropertyTools.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.Settings;
internal class SettingsPropertyGridFactory : PropertyGridControlFactory
{
    public override FrameworkElement CreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        if (property.Is(typeof(ICommand)))
        {
            var button = new Button();
            button.SetBinding(Button.CommandProperty, property.CreateBinding());
            button.SetCurrentValue(Button.ContentProperty, Resources.Run);
            return button;
        }
        return base.CreateControl(property, options);
    }
}
