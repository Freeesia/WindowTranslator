using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// PluginStoreView.xaml の相互作用ロジック
/// </summary>
public partial class PluginStoreView
{
    private bool loaded;

    public PluginStoreView()
    {
        InitializeComponent();
        this.IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (this.loaded || !this.IsVisible)
            return;
        this.loaded = true;
        if (this.DataContext is PluginStoreViewModel vm)
        {
            _ = vm.LoadCommand.ExecuteAsync(null);
        }
    }
}

/// <summary>
/// null でない場合に true を返すコンバーター
/// </summary>
[ValueConversion(typeof(object), typeof(bool))]
public sealed class NotNullToBoolConverter : IValueConverter
{
    public static NotNullToBoolConverter Default { get; } = new();

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// null でない場合に Visible を返すコンバーター
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public static NotNullToVisibilityConverter Default { get; } = new();

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is not null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// bool を反転するコンバーター（Visibility対応）
/// </summary>
[ValueConversion(typeof(bool), typeof(object))]
public sealed class InverseBoolConverter : IValueConverter
{
    public static InverseBoolConverter Default { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var inverseBool = value is bool b && !b;
        if (parameter is string p && p == "Visibility")
            return inverseBool ? Visibility.Visible : Visibility.Collapsed;
        return inverseBool;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}
