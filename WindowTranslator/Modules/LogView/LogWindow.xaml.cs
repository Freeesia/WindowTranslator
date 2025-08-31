using Wpf.Ui;
using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.LogView;
/// <summary>
/// LogWindow.xaml の相互作用ロジック
/// </summary>
public partial class LogWindow : FluentWindow
{
    public LogWindow(ISnackbarService snackbarService)
    {
        InitializeComponent();
        snackbarService.SetSnackbarPresenter(this.SnackbarPresenter);
    }
}
