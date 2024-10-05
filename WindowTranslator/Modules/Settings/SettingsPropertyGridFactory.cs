using PropertyTools.Wpf;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Modules.Settings;
internal class SettingsPropertyGridFactory(IEnumerable<IPropertyView> views) : PropertyGridControlFactory
{
    private readonly IEnumerable<IPropertyView> views = views;

    public override FrameworkElement CreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        if (property.Descriptor.GetAttributeValue<PropertyViewAttribute, IPropertyView?>(attr => views.FirstOrDefault(v => v.ViewName == attr.ViewName)) is { } view &&
            view.ViewType.IsAssignableTo(typeof(FrameworkElement)))
        {
            var control = (FrameworkElement)Activator.CreateInstance(view.ViewType)!;
            control.SetBinding(FrameworkElement.DataContextProperty, property.CreateOneWayBinding());
            return control;
        }
        else if (property.Is(typeof(ICommand)))
        {
            var button = new Button();
            button.SetBinding(ButtonBase.CommandProperty, property.CreateOneWayBinding());
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

    protected override FrameworkElement CreateSliderControl(PropertyItem property)
    {
        var grid = (Grid)base.CreateSliderControl(property);
        grid.ColumnDefinitions[1].SetCurrentValue(System.Windows.Controls.ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Auto));
        grid.ColumnDefinitions[1].SetCurrentValue(System.Windows.Controls.ColumnDefinition.MinWidthProperty, 80d);
        return grid;
    }

    protected override FrameworkElement CreateFontFamilyControl(PropertyItem property)
    {
        var comboBox = (ComboBox)base.CreateFontFamilyControl(property);
        var factory = comboBox.ItemTemplate.VisualTree;
        factory.SetBinding(TextBlock.TextProperty, new Binding() { Converter = FontFamilyDisplayNameConverter.Instance });
        var binding = property.CreateBinding();
        if (property.ActualPropertyType == typeof(string))
        {
            binding.Converter = FontFamilyConverter.Instance;
        }

        comboBox.SetBinding(Selector.SelectedValueProperty, binding);

        return comboBox;
    }

    [ValueConversion(typeof(FontFamily), typeof(string))]
    private class FontFamilyDisplayNameConverter : IValueConverter
    {
        public static readonly FontFamilyDisplayNameConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FontFamily family && targetType == typeof(string))
            {
                var lang = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
                return family.FamilyNames.TryGetValue(lang, out var d) ? d : family.Source;
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    [ValueConversion(typeof(string), typeof(FontFamily))]
    private class FontFamilyConverter : IValueConverter
    {
        public static readonly FontFamilyConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string source)
            {
                return new FontFamily(source);
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FontFamily family && targetType == typeof(string))
            {
                return family.Source;
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
