using System.Globalization;
using System.Windows.Markup;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.ErrorReport;
/// <summary>
/// ErrorReportDialog.xaml の相互作用ロジック
/// </summary>
public partial class ErrorReportDialog : FluentWindow
{
    public ErrorReportDialog(IContentDialogService contentDialogService)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        contentDialogService.SetDialogHost(this.RootContentDialog);
    }
}
