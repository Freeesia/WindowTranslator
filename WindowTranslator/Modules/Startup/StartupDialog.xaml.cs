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
    private bool activationRequested;

    public StartupDialog(IConfiguration configuration)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.mode = configuration.GetValue(nameof(LaunchMode), LaunchMode.Direct);
        SingleInstanceWindowActivator.Register(this, () =>
        {
            this.activationRequested = true;
            if (this.IsLoaded)
            {
                ActivateStartupDialog();
            }
        });
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (this.activationRequested)
        {
            ActivateStartupDialog();
        }
        else if (this.mode == LaunchMode.Startup)
        {
            this.SetCurrentValue(VisibilityProperty, Visibility.Hidden);
        }
    }

    private void ActivateStartupDialog()
    {
        this.activationRequested = false;
        Show();
        if (this.WindowState == WindowState.Minimized)
        {
            this.SetCurrentValue(WindowStateProperty, WindowState.Normal);
        }
        _ = Activate();
    }
}

public enum LaunchMode
{
    Direct,
    Startup,
}
