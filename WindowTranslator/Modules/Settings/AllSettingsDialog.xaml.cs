using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using WindowTranslator.ComponentModel;
using WindowTranslator.Stores;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.Settings;

/// <summary>
/// AllSettingsDialog.xaml の相互作用ロジック
/// </summary>
public partial class AllSettingsDialog : FluentWindow
{
    private readonly IModelHistoryStore modelHistoryStore;

    public AllSettingsDialog(IContentDialogService contentDialogService, IModelHistoryStore modelHistoryStore)
    {
        this.modelHistoryStore = modelHistoryStore;
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        contentDialogService.SetDialogHost(this.RootContentDialog);
        if (this.Resources["operator"] is SettingsPropertyGridOperator op)
        {
            op.HistoryStore = modelHistoryStore;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (this.DataContext is AllSettingsViewModel viewModel)
        {
            var cache = new Dictionary<Type, PropertyDescriptorCollection>();
            foreach (var target in viewModel.Targets)
            {
                foreach (var param in target.Params)
                {
                    var paramType = param.GetType();
                    if (!cache.TryGetValue(paramType, out var properties))
                    {
                        properties = TypeDescriptor.GetProperties(param);
                        cache[paramType] = properties;
                    }
                    foreach (PropertyDescriptor pd in properties)
                    {
                        var attr = pd.Attributes.OfType<EditableItemsSourceAttribute>().FirstOrDefault();
                        if (attr != null && pd.GetValue(param) is string value && !string.IsNullOrWhiteSpace(value))
                        {
                            this.modelHistoryStore.AddHistory($"{paramType.Name}.{pd.Name}", value);
                        }
                    }
                }
            }
            this.modelHistoryStore.Save();
        }
    }
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class FalseToVisibilityConverter : IValueConverter
{
    public static FalseToVisibilityConverter Default { get; } = new FalseToVisibilityConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v != Visibility.Visible;
}

[ValueConversion(typeof(string), typeof(string))]
public sealed class TargetNameConverter : IValueConverter
{
    public static TargetNameConverter Default { get; } = new TargetNameConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string { Length: > 0 } name ? name : "Default";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}