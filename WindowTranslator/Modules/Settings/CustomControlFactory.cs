using PropertyTools.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WindowTranslator.Modules.Settings;
internal class CustomControlFactory : PropertyGridControlFactory
{
    public override FrameworkElement CreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        if (property.Is(typeof(ICommand)))
        {
            return CreateCommandControl(property);
        }
        return base.CreateControl(property, options);
    }

    private FrameworkElement CreateCommandControl(PropertyItem property)
    {
        var button = new Button
        {
            Content = "実行",
            MinWidth = 100,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        button.SetBinding(Button.CommandProperty, property.CreateOneWayBinding());
        return button;
    }
}
