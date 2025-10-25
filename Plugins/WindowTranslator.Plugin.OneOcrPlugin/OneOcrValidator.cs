using WindowTranslator.Modules;
using WindowTranslator.Plugin.OneOcrPlugin.Properties;

namespace WindowTranslator.Plugin.OneOcrPlugin;

public class OneOcrValidator : ITargetSettingsValidator
{
    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(IOcrModule)] != nameof(OneOcr))
        {
            return ValidateResult.Valid;
        }

        // ScreenSketchのバージョンをチェック
        var (isVersionSufficient, message) = await Utility.CheckScreenSketchVersionAsync().ConfigureAwait(false);
        
        if (!isVersionSufficient && message != null)
        {
            // バージョンが古い場合、Microsoft Storeを開いて更新を促す
            Utility.OpenStoreForUpdate();
            
            // ストアを開いた後、定期的にバージョンをチェック（最大30秒間、5秒ごと）
            var maxRetries = 6;
            var retryDelay = TimeSpan.FromSeconds(5);
            
            for (int i = 0; i < maxRetries; i++)
            {
                await Task.Delay(retryDelay).ConfigureAwait(false);
                
                var (newVersionSufficient, _) = await Utility.CheckScreenSketchVersionAsync().ConfigureAwait(false);
                if (newVersionSufficient)
                {
                    // 更新が完了した
                    break;
                }
            }
            
            // 最終確認
            var (finalVersionSufficient, finalMessage) = await Utility.CheckScreenSketchVersionAsync().ConfigureAwait(false);
            if (!finalVersionSufficient)
            {
                // まだバージョンが古い場合はエラーを返す
                var errorMessage = finalMessage != null
                    ? string.Format(Resources.NotFoundModuleWithVersion, finalMessage)
                    : Resources.NotFoundModule;
                return ValidateResult.Invalid("OneOcr", errorMessage);
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