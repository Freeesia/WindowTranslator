using PropertyTools.Wpf;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

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

    protected override FrameworkElement CreateDefaultControl(PropertyItem property)
    {
        var textBoxEx = new Wpf.Ui.Controls.TextBox
        {
            AcceptsReturn = property.AcceptsReturn,
            MaxLength = property.MaxLength,
            IsReadOnly = property.IsReadOnly,
            TextWrapping = property.TextWrapping,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = property.AcceptsReturn ? VerticalAlignment.Top : VerticalAlignment.Center,
            PlaceholderText = property.Description,
        };
        if (property.FontFamily != null)
        {
            textBoxEx.FontFamily = new FontFamily(property.FontFamily);
        }

        if (!double.IsNaN(property.FontSize))
        {
            textBoxEx.FontSize = property.FontSize;
        }

        if (property.IsReadOnly)
        {
            textBoxEx.Foreground = Brushes.RoyalBlue;
        }

        var binding = property.CreateBinding(property.AutoUpdateText ? UpdateSourceTrigger.PropertyChanged : UpdateSourceTrigger.Default);
        if (property.ActualPropertyType != typeof(string) && IsNullable(property.ActualPropertyType))
        {
            binding.TargetNullValue = string.Empty;
        }

        textBoxEx.SetBinding(TextBox.TextProperty, binding);
        return textBoxEx;
    }

    private static bool IsNullable(Type type)
    {
        if (!type.IsValueType)
        {
            return true;
        }

        if (Nullable.GetUnderlyingType(type) != null)
        {
            return true;
        }

        return false;
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
