using PropertyTools.DataAnnotations;
using PropertyTools.Wpf;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using WindowTranslator.ComponentModel;
using WindowTranslator.Controls;
using WindowTranslator.Data;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace WindowTranslator.Modules.Settings;

internal class SettingsPropertyGridFactory : PropertyGridControlFactory
{
    private static readonly EnableAttribute enableAttribute = new(true);
    private static readonly EnableAttribute disableAttribute = new(false);

    public override FrameworkElement CreateControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        FrameworkElement? fe = null;
        if (property.Is(typeof(ICommand)))
        {
            fe = new Button();
            fe.SetBinding(ButtonBase.CommandProperty, property.CreateOneWayBinding());
            fe.SetCurrentValue(ContentControl.ContentProperty, property.DisplayName);
            fe.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Stretch);
        }

        // MEMO: 本当は属性とか介して判定した方がいい
        if (property.PropertyName == nameof(TargetSettingsViewModel.OverlayShortcut))
        {
            fe = new ShortcutBox();
            fe.SetBinding(TextBox.TextProperty, property.CreateBinding());
        }

        fe ??= base.CreateControl(property, options);

        // マウスポインター判定の余白は、コントロールにフォーカスがある間だけ設定画面内にプレビュー表示する
        if (property.PropertyName == nameof(TargetSettingsViewModel.MousePointerHitTestPadding))
        {
            AttachMousePointerHitTestPaddingPreview(fe);
        }

        if (property.Descriptor.Attributes.Matches(enableAttribute))
        {
            fe.IsEnabled = true;
        }
        else if (property.Descriptor.Attributes.Matches(disableAttribute))
        {
            fe.IsEnabled = false;
        }

        // HelpUriAttributeが定義されている場合、ヘルプボタンを追加
        var helpUriAttr = property.Descriptor.Attributes.OfType<HelpUriAttribute>().FirstOrDefault();
        if (helpUriAttr != null)
        {
            fe = WrapWithHelpButton(fe, helpUriAttr.PageName);
        }

        return fe;
    }

    /// <summary>
    /// <paramref name="fe"/>にフォーカスがある間、<see cref="TargetSettingsViewModel.MousePointerHitTestPadding"/>の
    /// 現在値に応じた大きさのプレビュー矩形を、マウスカーソル位置に追従するAdornerとして表示する。
    /// NumberBoxやSliderなど、生成されたコントロールの種類を問わず動作する。
    /// </summary>
    private static void AttachMousePointerHitTestPaddingPreview(FrameworkElement fe)
    {
        MousePointerHitTestPaddingAdorner? adorner = null;
        AdornerLayer? layer = null;
        Window? window = null;
        PropertyChangedEventHandler? propertyChangedHandler = null;
        MouseEventHandler? mouseMoveHandler = null;

        fe.GotFocus += (_, _) =>
        {
            if (fe.DataContext is not TargetSettingsViewModel vm)
            {
                return;
            }
            layer = AdornerLayer.GetAdornerLayer(fe);
            if (layer is null)
            {
                return;
            }
            adorner = new MousePointerHitTestPaddingAdorner(fe);
            layer.Add(adorner);
            adorner.Update(Mouse.GetPosition(fe), vm.MousePointerHitTestPadding);

            // スライダー操作中などマウスキャプチャが発生していても取得できるよう、Window単位でマウス移動を監視する
            window = Window.GetWindow(fe);
            mouseMoveHandler = (_, args) => adorner?.Update(args.GetPosition(fe), vm.MousePointerHitTestPadding);
            if (window is not null)
            {
                window.PreviewMouseMove += mouseMoveHandler;
            }

            propertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(TargetSettingsViewModel.MousePointerHitTestPadding))
                {
                    adorner?.Update(Mouse.GetPosition(fe), vm.MousePointerHitTestPadding);
                }
            };
            vm.PropertyChanged += propertyChangedHandler;
        };
        fe.LostFocus += (_, _) =>
        {
            if (fe.DataContext is TargetSettingsViewModel vm && propertyChangedHandler is not null)
            {
                vm.PropertyChanged -= propertyChangedHandler;
                propertyChangedHandler = null;
            }
            if (window is not null && mouseMoveHandler is not null)
            {
                window.PreviewMouseMove -= mouseMoveHandler;
                mouseMoveHandler = null;
            }
            if (layer is not null && adorner is not null)
            {
                layer.Remove(adorner);
            }
            adorner = null;
            layer = null;
            window = null;
        };
    }

    /// <summary>
    /// マウスカーソル位置を中心に、当たり判定の余白と同じ計算(<see cref="TextOverlayVisibilityConverter.InflatePadding"/>)で
    /// 拡張した矩形を描画するAdorner。表示される範囲と実際の判定範囲を一致させる。
    /// </summary>
    private sealed class MousePointerHitTestPaddingAdorner : Adorner
    {
        private static readonly Brush FillBrush;
        private static readonly Pen BorderPen;

        static MousePointerHitTestPaddingAdorner()
        {
            FillBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x7A, 0xCC));
            FillBrush.Freeze();
            BorderPen = new Pen(Brushes.DodgerBlue, 1.5);
            BorderPen.Freeze();
        }

        private Point mousePosition;
        private double padding;

        public MousePointerHitTestPaddingAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        public void Update(Point position, double padding)
        {
            this.mousePosition = position;
            this.padding = padding;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var rect = TextOverlayVisibilityConverter.InflatePadding(new Rect(mousePosition, mousePosition), padding);
            drawingContext.DrawRectangle(FillBrush, BorderPen, rect);
        }
    }

    private static Grid WrapWithHelpButton(FrameworkElement control, string pageName)
    {
        var grid = new Grid();

        // 2列定義: コントロール列とボタン列
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });

        // コントロールを配置
        control.SetCurrentValue(Grid.ColumnProperty, 0);
        grid.Children.Add(control);

        // 現在のUI言語に基づいてURLを構築
        var helpUri = BuildHelpUri(pageName);

        // ヘルプボタンを配置
        var helpButton = new HyperlinkButton
        {
            Content = new SymbolIcon(SymbolRegular.QuestionCircle48)
            {
                Foreground = Brushes.DodgerBlue,
                Filled = true,
                FontSize = 24,
            },
            ToolTip = "Help",
            Padding = new(4),
            CornerRadius = new(16),
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            NavigateUri = helpUri,
        };
        helpButton.SetCurrentValue(Grid.ColumnProperty, 1);
        grid.Children.Add(helpButton);

        return grid;
    }

    private static string BuildHelpUri(string pageName)
    {
        const string baseUrl = "https://wt.studiofreesia.com/";

        // 現在のUI言語を取得
        var culture = CultureInfo.CurrentUICulture;

        // 言語コードをドキュメントファイルのサフィックスにマッピング
        var languageSuffix = culture.Name switch
        {
            "ja-JP" or "ja" => "", // 日本語はデフォルト（サフィックスなし）
            "zh-Hans" or "zh-CN" => ".zh-cn",
            "zh-Hant" or "zh-TW" => ".zh-tw",
            _ => "." + culture.TwoLetterISOLanguageName.ToLowerInvariant(),
        };

        return $"{baseUrl}{pageName}{languageSuffix}";
    }

    private class SymbolIcon(SymbolRegular symbol) : Wpf.Ui.Controls.SymbolIcon(symbol)
    {

        protected override UIElement InitializeChildren()
        {
            var ret = base.InitializeChildren();
            // Foregroundが反映されない問題の対策
            ret.SetCurrentValue(System.Windows.Controls.TextBlock.ForegroundProperty, this.Foreground);
            return ret;
        }
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

    protected override FrameworkElement CreateLinkControl(PropertyItem property)
    {
        var fe = base.CreateLinkControl(property);
        fe.SetResourceReference(TextBlock.ForegroundProperty, "HyperlinkButtonForeground");
        fe.SetCurrentValue(TextBlock.TextDecorationsProperty, TextDecorations.Underline);
        if (!string.IsNullOrEmpty(property.Description))
        {
            fe.SetValue(TextBlock.TextProperty, property.Description);
        }
        return fe;
    }

    protected override FrameworkElement CreateSpinControl(PropertyItem property)
    {
        if (property.Is(typeof(byte)) ||
            property.Is(typeof(sbyte)) ||
            property.Is(typeof(ushort)) ||
            property.Is(typeof(short)) ||
            property.Is(typeof(uint)) ||
            property.Is(typeof(int)) ||
            property.Is(typeof(ulong)) ||
            property.Is(typeof(long)) ||
            property.Is(typeof(float)) ||
            property.Is(typeof(double)))
        {
            var numberBox = new NumberBox();
            numberBox.Maximum = Convert.ToDouble(property.SpinMaximum, CultureInfo.CurrentCulture);
            numberBox.Minimum = Convert.ToDouble(property.SpinMinimum, CultureInfo.CurrentCulture);
            numberBox.SmallChange = Convert.ToDouble(property.SpinSmallChange, CultureInfo.CurrentCulture);
            numberBox.LargeChange = Convert.ToDouble(property.SpinLargeChange, CultureInfo.CurrentCulture);
            numberBox.SetBinding(NumberBox.ValueProperty, property.CreateBinding(UpdateSourceTrigger.PropertyChanged));
            return numberBox;
        }
        else
        {
            return base.CreateSpinControl(property);
        }
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

        var binding = property.CreateBinding(property.AutoUpdateText ? UpdateSourceTrigger.PropertyChanged : UpdateSourceTrigger.Default);
        if (property.ActualPropertyType != typeof(string) && IsNullable(property.ActualPropertyType))
        {
            binding.TargetNullValue = string.Empty;
        }

        textBoxEx.SetBinding(TextBox.TextProperty, binding);
        return textBoxEx;
    }

    private static List<EnumItem> GetEnumValues2(Type enumType)
    {
        var underlyingType = Nullable.GetUnderlyingType(enumType);
        if (underlyingType != null)
        {
            enumType = underlyingType;
        }

        var list = EnumItem.GetEnumItems(enumType).ToList();
        if (underlyingType != null)
        {
            list.Add(new EnumItem(enumType, null));
        }

        return list;
    }

    protected override FrameworkElement CreateEnumControl(PropertyItem property, PropertyControlFactoryOptions options)
    {
        var array = GetEnumValues2(property.Descriptor.PropertyType);
        var selectorStyle = property.SelectorStyle;
        if (selectorStyle == SelectorStyle.Auto)
        {
            selectorStyle = (array.Count <= options.EnumAsRadioButtonsLimit) ? SelectorStyle.RadioButtons : SelectorStyle.ComboBox;
        }

        switch (selectorStyle)
        {
            case SelectorStyle.RadioButtons:
                var radioButtonList = new RadioButtonList();
                radioButtonList.EnumType = property.Descriptor.PropertyType;
                radioButtonList.SetBinding(RadioButtonList.ValueProperty, property.CreateBinding());
                return radioButtonList;
            case SelectorStyle.ComboBox:
                var comboBox = new ComboBox();
                comboBox.ItemsSource = array;
                comboBox.DisplayMemberPath = nameof(EnumItem.Display);
                comboBox.SelectedValuePath = nameof(EnumItem.Value);
                comboBox.SetBinding(Selector.SelectedValueProperty, property.CreateBinding());
                return comboBox;
            case SelectorStyle.ListBox:
                var listBox = new ListBox();
                listBox.ItemsSource = array;
                listBox.DisplayMemberPath = nameof(EnumItem.Display);
                listBox.SelectedValuePath = nameof(EnumItem.Value);
                listBox.SetBinding(Selector.SelectedValueProperty, property.CreateBinding());
                return listBox;
            default:
                throw new InvalidOperationException();
        }
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

    private record EnumItem(Type Type, object? Value)
    {
        public string? Display { get; } = GetDisplay(Type, Value);

        private static string? GetDisplay(Type type, object? value)
        {
            if (value == null)
            {
                return "-";
            }
            var name = Enum.GetName(type, value) ?? throw new InvalidOperationException();
            var field = type.GetField(name) ?? throw new InvalidOperationException();
            if (field.GetCustomAttribute<LocalizedDescriptionAttribute>()?.Description is { } desc)
            {
                return desc;
            }
            if (field.GetCustomAttribute<DisplayAttribute>()?.Name is { } disp)
            {
                return disp;
            }
            if (type.GetResourceManager()?.GetString(name, CultureInfo.CurrentCulture) is { } resText)
            {
                return resText;
            }

            return name;
        }

        public static IEnumerable<EnumItem> GetEnumItems(Type type)
        {
            foreach (var value in Enum.GetValues(type).FilterOnBrowsableAttribute())
            {
                yield return new EnumItem(type, value);
            }
        }
    }

}
