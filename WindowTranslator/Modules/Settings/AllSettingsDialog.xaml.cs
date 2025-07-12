using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.Settings;

/// <summary>
/// AllSettingsDialog.xaml の相互作用ロジック
/// </summary>
public partial class AllSettingsDialog : FluentWindow
{
    public AllSettingsDialog(IContentDialogService contentDialogService)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        contentDialogService.SetDialogHost(this.RootContentDialog);
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