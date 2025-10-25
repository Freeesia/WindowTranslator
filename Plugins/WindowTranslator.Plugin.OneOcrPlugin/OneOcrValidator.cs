using WindowTranslator.Modules;
using WindowTranslator.Plugin.OneOcrPlugin.Properties;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace WindowTranslator.Plugin.OneOcrPlugin;

public class OneOcrValidator(IContentDialogService dialogService) : ITargetSettingsValidator
{
    private static readonly ValidateResult Invalid = ValidateResult.Invalid("OneOcr", Resources.NotFoundModule);
    private readonly IContentDialogService dialogService = dialogService;

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(IOcrModule)] != nameof(OneOcr))
        {
            return ValidateResult.Valid;
        }

        // ScreenSketchのバージョンをチェック       
        if (!await Utility.CheckScreenSketchVersionAsync().ConfigureAwait(false))
        {
            // バージョンが古い場合、確認ダイアログを表示
            var confirmResult = await this.dialogService.ShowSimpleDialogAsync(new()
            {
                Title = "切り取り領域とスケッチの更新",
                Content = """
                OneOcrを利用するには「切り取り領域とスケッチ」アプリの更新が必要です。
                
                Microsoft Storeを開いて更新を行いますか？
                
                > #### オプション
                > [手動で更新する](ms-windows-store://pdp/?ProductId=9MZ95KL8MR0L)
                """,
                PrimaryButtonText = "Storeを開く",
                CloseButtonText = "更新しない"
            });

            if (confirmResult != ContentDialogResult.Primary)
            {
                return Invalid;
            }

            // Microsoft Storeを開く
            Utility.OpenStoreForUpdate();

            // 更新チェック用のキャンセルトークン
            var checkCts = new CancellationTokenSource();
            var dialogCts = new CancellationTokenSource();
            
            // バックグラウンドでバージョンチェックを継続
            var checkTask = Task.Run(async () =>
            {
                while (!checkCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), checkCts.Token).ConfigureAwait(false);
                    
                    if (await Utility.CheckScreenSketchVersionAsync().ConfigureAwait(false))
                    {
                        // 更新完了
                        dialogCts.Cancel();
                        return;
                    }
                }
            }, checkCts.Token);

            try
            {
                // 更新中ダイアログを表示
                await this.dialogService.ShowSimpleDialogAsync(new()
                {
                    Title = "更新確認中...",
                    Content = """
                    「切り取り領域とスケッチ」の更新を確認中...
                    
                    Microsoft Storeで更新を完了してください。
                    更新が完了すると自動的に次に進みます。
                    
                    > #### 更新が完了しない場合
                    > お手数ですが、Microsoft Storeで手動更新を完了してから再度お試しください。
                    > [Microsoft Storeを開く](ms-windows-store://pdp/?ProductId=9MZ95KL8MR0L)
                    """,
                    CloseButtonText = "中断"
                }, dialogCts.Token);
                
                // ユーザーが中断した場合
                await checkCts.CancelAsync();
                return Invalid;
            }
            catch (OperationCanceledException)
            {
                // 更新完了によるダイアログのキャンセル
            }

            try
            {
                await checkTask;
            }
            catch (OperationCanceledException)
            {
                // タスクのキャンセル
            }
        }

        if (!Utility.NeedCopyDll())
        {
            return ValidateResult.Valid;
        }

        // OneOcrのインストール先を取得
        var oneOcrPath = await Utility.FindOneOcrPath().ConfigureAwait(false);
        if (oneOcrPath == null)
        {
            return ValidateResult.Invalid("OneOcr", Resources.NotFoundModule);
        }

        // DLLをコピー
        try
        {
            Utility.CopyDll(oneOcrPath);
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid("OneOcr", string.Format(Resources.CopyFaild, ex.Message));
        }

        return ValidateResult.Valid;
    }
}