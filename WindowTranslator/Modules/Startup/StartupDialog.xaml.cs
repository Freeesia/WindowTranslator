using Microsoft.Extensions.Configuration;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.Startup;
/// <summary>
/// StartupDialog.xaml の相互作用ロジック
/// </summary>
public partial class StartupDialog : FluentWindow
{
    private readonly LaunchMode mode;

    public StartupDialog(IConfiguration configuration)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.mode = configuration.GetValue(nameof(LaunchMode), LaunchMode.Direct);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.mode == LaunchMode.Startup)
        {
            this.SetCurrentValue(VisibilityProperty, Visibility.Hidden);
        }
    }
}

public enum LaunchMode
{
    Direct,
    Startup,
}