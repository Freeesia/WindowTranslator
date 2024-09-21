using System.Globalization;
using System.Windows;
using MdXaml;
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
        this.text.Markdown = $"""
            翻訳元言語「{culture.DisplayName}」は文字認識のために必要なOCR機能がインストールされていません。

            インストールを行いますか？

            > #### オプション
            > [手動でインストールする](ms-settings:regionlanguage?activationSource=SMC-Article-14236) 
            > ([手順](https://github.com/Freeesia/WindowTranslator/wiki/%E6%89%8B%E5%8B%95%E3%81%A7%E8%A8%80%E8%AA%9E%E3%83%91%E3%83%83%E3%82%AF%E3%82%92%E3%82%A4%E3%83%B3%E3%82%B9%E3%83%88%E3%83%BC%E3%83%AB%E3%81%99%E3%82%8B))
            """;
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        button.Content = "インストール中...";
        this.progress.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        var culture = new CultureInfo(lang);
        this.text.SetCurrentValue(MarkdownScrollViewer.MarkdownProperty, $"""
            「{culture.DisplayName}」の言語パックをインストール中...  
            5～10分程度かかる場合があります。  

            再起動が促された場合は再起動してください。  
            
            > #### オプション
            > [手動でインストールする](ms-settings:regionlanguage?activationSource=SMC-Article-14236) 
            > ([手順](https://github.com/Freeesia/WindowTranslator/wiki/%E6%89%8B%E5%8B%95%E3%81%A7%E8%A8%80%E8%AA%9E%E3%83%91%E3%83%83%E3%82%AF%E3%82%92%E3%82%A4%E3%83%B3%E3%82%B9%E3%83%88%E3%83%BC%E3%83%AB%E3%81%99%E3%82%8B))
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
