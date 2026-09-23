using System.Globalization;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Modules.Ocr;

public class WindowsMediaOcrValidator(IContentDialogService dialogService) : ITargetSettingsValidator
{
    private static readonly ValidateResult Invalid = ValidateResult.Invalid("文字認識機能", "文字認識に必要な機能がインストールされていません");
    private readonly IContentDialogService dialogService = dialogService;

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(IOcrModule)] != nameof(WindowsMediaOcr))
        {
            return ValidateResult.Valid;
        }
        var culture = new CultureInfo(settings.Language.Source);
        try
        {
            if (WindowsMediaOcrUtility.IsInstalledLanguage(settings.Language.Source))
            {
                return ValidateResult.Valid;
            }
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid("文字認識機能",
                $"""
                ORC機能のインストール状態の取得に失敗しました。  
                    
                [こちら](ms-settings:regionlanguage?activationSource=SMC-Article-14236)から翻訳元言語「{culture.DisplayName}」がインストールされているか確認してください。
                インストールされていない場合は手動でインストールを行ってください。
                ([インストール手順](https://github.com/Freeesia/WindowTranslator/wiki/%E6%89%8B%E5%8B%95%E3%81%A7%E8%A8%80%E8%AA%9E%E3%83%91%E3%83%83%E3%82%AF%E3%82%92%E3%82%A4%E3%83%B3%E3%82%B9%E3%83%88%E3%83%BC%E3%83%AB%E3%81%99%E3%82%8B))
                
                #### エラー内容
                ```
                {ex.Message}
                ```
                """);
        }

        var r = await this.dialogService.ShowSimpleDialogAsync(new()
        {
            Title = "文字認識機能",
            Content = $"""
            翻訳元言語「{culture.DisplayName}」は文字認識のために必要なOCR機能がインストールされていません。
            
            インストールを行いますか？
            
            > #### オプション
            > [手動でインストールする](ms-settings:regionlanguage?activationSource=SMC-Article-14236) 
            > ([手順](https://github.com/Freeesia/WindowTranslator/wiki/%E6%89%8B%E5%8B%95%E3%81%A7%E8%A8%80%E8%AA%9E%E3%83%91%E3%83%83%E3%82%AF%E3%82%92%E3%82%A4%E3%83%B3%E3%82%B9%E3%83%88%E3%83%BC%E3%83%AB%E3%81%99%E3%82%8B))
            """,
            PrimaryButtonText = "インストールする",
            CloseButtonText = "インストールしない"
        });

        if (r != ContentDialogResult.Primary)
        {
            return Invalid;
        }

        var installCts = new CancellationTokenSource();
        var dialogCts = new CancellationTokenSource();
        var task = WindowsMediaOcrUtility.InstallLanguageAsync(settings.Language.Source, installCts.Token)
            .ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    dialogCts.Cancel();
                }
            }, TaskScheduler.Default);
        try
        {
            await this.dialogService.ShowSimpleDialogAsync(new()
            {
                Title = "文字認識機能のインストール中...",
                Content = $"""
                「{culture.DisplayName}」の言語パックをインストール中...
                5～10分程度かかる場合があります。
            
                再起動が促された場合は再起動してください。
            
                > #### インストールが完了しない場合
                > お手数ですが、手動でのインストールを試してください。
                > [手動でインストールする](ms-settings:regionlanguage?activationSource=SMC-Article-14236)
                > ([手順](https://github.com/Freeesia/WindowTranslator/wiki/%E6%89%8B%E5%8B%95%E3%81%A7%E8%A8%80%E8%AA%9E%E3%83%91%E3%83%83%E3%82%AF%E3%82%92%E3%82%A4%E3%83%B3%E3%82%B9%E3%83%88%E3%83%BC%E3%83%AB%E3%81%99%E3%82%8B))
                """,
                CloseButtonText = "中断"
            }, dialogCts.Token);
            await installCts.CancelAsync();
            return Invalid;
        }
        catch (OperationCanceledException)
        {
        }
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }

        return ValidateResult.Valid;
    }
}