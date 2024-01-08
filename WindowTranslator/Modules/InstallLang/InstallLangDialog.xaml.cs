using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.InstallLang;

/// <summary>
/// InstallLangDialog.xaml の相互作用ロジック
/// </summary>
public partial class InstallLangDialog : FluentWindow
{
    public InstallLangDialog()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
    }
}
