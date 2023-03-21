﻿using PropertyTools.Wpf;
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
            button.SetBinding(Button.CommandProperty, property.CreateBinding());
            button.SetCurrentValue(Button.ContentProperty, "実行");
            return button;
        }
        return base.CreateControl(property, options);
    }
}