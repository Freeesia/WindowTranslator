using HwndExtensions.Utils;
using PropertyTools.Wpf;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Wpf.Ui.Markup;

namespace WindowTranslator.Modules.Settings;
internal class SettingsPropertyGridFactory : PropertyGridControlFactory
{
    public override FrameworkElement CreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        if (property.Is(typeof(ICommand)))
        {
            var button = new Button();
            button.SetBinding(ButtonBase.CommandProperty, property.CreateOneWayBinding());
            button.SetCurrentValue(ContentControl.ContentProperty, property.DisplayName);
            button.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            return button;
        }
        else if (property.PropertyName == nameof(SettingsViewModel.AutoTargets))
        {
            var list = new Wpf.Ui.Controls.ListView();
            list.SetBinding(ItemsControl.ItemsSourceProperty, property.CreateOneWayBinding());
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding());
            var button = new FrameworkElementFactory(typeof(Wpf.Ui.Controls.Button));
            button.SetValue(Wpf.Ui.Controls.Button.IconProperty, new SymbolIconExtension(Wpf.Ui.Controls.SymbolRegular.Delete24));
            button.SetValue(DockPanel.DockProperty, Dock.Right);
            button.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            button.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                var button = s as FrameworkElement;
                var item = button.TryFindVisualAncestor<Wpf.Ui.Controls.ListViewItem>()?.DataContext as string;
                var list = button.TryFindVisualAncestor<Wpf.Ui.Controls.ListView>()?.ItemsSource as IList<string>;
                if (list is not null && item is not null)
                {
                    list.Remove(item);
                }
            }));

            var dock = new FrameworkElementFactory(typeof(DockPanel));
            dock.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 8, 0));
            dock.AppendChild(button);
            dock.AppendChild(text);
            list.SetCurrentValue(ItemsControl.ItemTemplateProperty, new DataTemplate() { VisualTree = dock });
            list.SetCurrentValue(FrameworkElement.HeightProperty, 400d);
            return list;
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
