using System.Windows.Data;
using System.Windows;
using Wpf.Ui.Controls;
using System.Globalization;

namespace WindowTranslator.Modules.Settings;
/// <summary>
/// AllSettingsDialog.xaml の相互作用ロジック
/// </summary>
public partial class AllSettingsDialog : FluentWindow
{
    public AllSettingsDialog()
        => InitializeComponent();
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