using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace WindowTranslator.Modules.Validate;

/// <summary>
/// ValidateDialog.xaml の相互作用ロジック
/// </summary>
public partial class ValidateDialog : Window
{
    private readonly IContentDialogService contentDialogService;

    public ValidateDialog(IContentDialogService contentDialogService)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.contentDialogService = contentDialogService;
        this.Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        contentDialogService.SetDialogHost(this.RootContentDialog);
    }
}
