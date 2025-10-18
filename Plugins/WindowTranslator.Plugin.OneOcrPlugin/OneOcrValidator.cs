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

        // ScreenSketchのバージョンをチェックし、必要に応じて更新
        var (success, message) = await Utility.CheckAndUpdateScreenSketchAsync().ConfigureAwait(false);
        if (!success && message != null)
        {
            // バージョンチェックや更新に失敗した場合は警告を表示するが、DLLのコピーは試みる
            // （既に新しいバージョンがインストールされている可能性があるため）
        }

        if (!Utility.NeedCopyDll())
        {
            return ValidateResult.Valid;
        }

        // OneOcrのインストール先を取得
        var oneOcrPath = await Utility.FindOneOcrPath().ConfigureAwait(false);
        if (oneOcrPath == null)
        {
            var errorMessage = message != null
                ? string.Format(Resources.NotFoundModuleWithVersion, message)
                : Resources.NotFoundModule;
            return ValidateResult.Invalid("OneOcr", errorMessage);
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