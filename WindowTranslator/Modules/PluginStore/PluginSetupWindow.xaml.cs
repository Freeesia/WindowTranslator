using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.PluginStore;

public partial class PluginSetupWindow : FluentWindow
{
    private readonly PluginSetupViewModel viewModel;

    internal PluginSetupWindow(PluginSetupViewModel viewModel)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        this.DataContext = viewModel;
        this.SetCurrentValue(MinWidthProperty, Math.Min(this.MinWidth, SystemParameters.WorkArea.Width));
        this.SetCurrentValue(MinHeightProperty, Math.Min(this.MinHeight, SystemParameters.WorkArea.Height));
        this.SetCurrentValue(WidthProperty, Math.Min(this.Width, SystemParameters.WorkArea.Width));
        this.SetCurrentValue(HeightProperty, Math.Min(this.Height, SystemParameters.WorkArea.Height));
        SystemThemeWatcher.Watch(this);
        SingleInstanceWindowActivator.Register(this, () =>
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.SetCurrentValue(WindowStateProperty, WindowState.Normal);
            }
            _ = Activate();
        });
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        // Alt+F4などでも未完了のセットアップを閉じず、スキップ操作で確定する。
        e.Cancel |= !this.viewModel.IsCompleted;
    }

    protected override void OnClosed(EventArgs e)
    {
        this.viewModel.Dispose();
        base.OnClosed(e);
    }
}
