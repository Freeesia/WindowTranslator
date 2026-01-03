using Microsoft.Extensions.Logging;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.OneOcrPlugin.Properties;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Plugin.OneOcrPlugin;

public class OneOcrValidator(IContentDialogService dialogService, ILogger<OneOcrValidator> logger) : ITargetSettingsValidator
{
    private static readonly ValidateResult Invalid = ValidateResult.Invalid(Resources.OneOcr, Resources.NotFoundModule);
    private readonly IContentDialogService dialogService = dialogService;
    private readonly ILogger<OneOcrValidator> logger = logger;
    private bool versionChecked = false;

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(IOcrModule)] != nameof(OneOcr))
        {
            return ValidateResult.Valid;
        }

        // ScreenSketchのバージョンをチェック       
        if (!this.versionChecked && !await Utility.CheckScreenSketchVersionAsync(this.logger))
        {
            // バージョンが古い場合、確認ダイアログを表示
            var confirmResult = await this.dialogService.ShowSimpleDialogAsync(new()
            {
                Title = Resources.UpdateDialogTitle,
                Content = Resources.UpdateDialogContent,
                PrimaryButtonText = Resources.OpenStoreButton,
                CloseButtonText = Resources.DoNotUpdateButton
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
                    await Task.Delay(TimeSpan.FromSeconds(5), checkCts.Token);

                    if (await Utility.CheckScreenSketchVersionAsync(this.logger))
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
                    Title = Resources.CheckingUpdateTitle,
                    Content = Resources.CheckingUpdateContent,
                    CloseButtonText = Resources.AbortButton
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

        this.versionChecked = true;

        if (!Utility.NeedCopyDll())
        {
            return ValidateResult.Valid;
        }

        // OneOcrのインストール先を取得
        var oneOcrPath = await Utility.FindOneOcrPath();
        if (oneOcrPath == null)
        {
            return Invalid;
        }

        // DLLをコピー
        try
        {
            Utility.CopyDll(oneOcrPath);
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid(Resources.OneOcr, string.Format(Resources.CopyFaild, ex.Message));
        }

        return ValidateResult.Valid;
    }
}