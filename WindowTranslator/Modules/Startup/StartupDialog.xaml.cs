using Microsoft.Extensions.Configuration;
using System.Windows;

namespace WindowTranslator.Modules.Startup;
/// <summary>
/// StartupDialog.xaml の相互作用ロジック
/// </summary>
public partial class StartupDialog : Window
{
    private readonly LaunchMode mode;

    public StartupDialog(IConfiguration configuration)
    {
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