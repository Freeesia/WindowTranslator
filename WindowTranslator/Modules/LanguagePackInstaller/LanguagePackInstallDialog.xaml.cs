using System.Globalization;
using System.Windows;
using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.LanguagePackInstaller;
/// <summary>
/// LanguagePackInstallDialog.xaml の相互作用ロジック
/// </summary>
public partial class LanguagePackInstallDialog : FluentWindow
{
    private readonly string lang;

    public LanguagePackInstallDialog(string lang)
    {
        InitializeComponent();
        this.lang = lang;
        
        var culture = new CultureInfo(lang);
        this.text.Text = $"""
            翻訳元言語「{culture.DisplayName}」は文字認識のために必要なOCR機能がインストールされていません。

            インストールを行いますか？
            """;
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        button.Content = "インストール中...";
        this.progress.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        var culture = new CultureInfo(lang);
        this.text.SetCurrentValue(TextBlock.TextProperty, $"""
            {culture.DisplayName} の言語パックをインストール中...
            5,6分程度かかる場合があります。

            再起動が促された場合は再起動してください。
            """);
        try
        {
            await LanguagePackUtility.InstallLanguageAsync(this.lang);
        }
        catch (OperationCanceledException)
        {
        }
        Close();
    }
}
