using WindowTranslator.Modules;

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

        if (!Utility.NeedCopyDll())
        {
            return ValidateResult.Valid;
        }

        // OneOcrのインストール先を取得
        var oneOcrPath = await Utility.FindOneOcrPath().ConfigureAwait(false);
        if (oneOcrPath == null)
        {
            return ValidateResult.Invalid("OneOcr", "依存モジュールが見つかりません。この環境では利用できません。");
        }

        // DLLをコピー
        try
        {
            Utility.CopyDll(oneOcrPath);
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid("OneOcr", $"OneOcrのDLLのコピーに失敗しました。{ex.Message}");
        }

        return ValidateResult.Valid;
    }
}