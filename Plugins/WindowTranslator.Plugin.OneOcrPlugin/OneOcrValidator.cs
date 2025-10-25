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
        if (!await Utility.CheckScreenSketchVersionAsync().ConfigureAwait(false))
        {
            // バージョンが古い場合、Microsoft Storeを開いて更新を促す
            Utility.OpenStoreForUpdate();            
            while (!await Utility.CheckScreenSketchVersionAsync().ConfigureAwait(false))
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);                
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