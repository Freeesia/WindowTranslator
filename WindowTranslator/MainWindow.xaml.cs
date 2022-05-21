using System.ComponentModel;
using System.Windows;

namespace WindowTranslator;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(IntPtr windowHandle)
    {
        InitializeComponent();
        this.DataContext = new MainViewModel(windowHandle);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            var vm = this.DataContext as MainViewModel;
            vm?.Dispose();
        }
    }
}
