using WindowTranslator.Modules;
using WindowTranslator.Properties;

namespace WindowTranslator.Extensions;

/// <summary>
/// 翻訳対象設定バリデーターの拡張メソッド
/// </summary>
public static class TargetSettingsValidatorExtensions
{
    /// <summary>
    /// 設定の検証を行う
    /// </summary>
    /// <param name="validators">バリデーターのコレクション</param>
    /// <param name="settings">検証する設定</param>
    /// <returns>検証結果のリスト（空の場合は全て有効）</returns>
    public static async Task<IReadOnlyList<ValidateResult>> ValidateAsync(
        this IEnumerable<ITargetSettingsValidator> validators,
        TargetSettings settings)
    {
        var results = new List<ValidateResult>();

        // 翻訳元言語と翻訳先言語が同じでないかチェック
        if (settings.Language.Source == settings.Language.Target)
        {
            results.Add(ValidateResult.Invalid(Resources.TranslateLanguage, Resources.SameSourceTargetLanguage));
        }

        // 翻訳モジュールが選択されているかチェック
        if (!settings.SelectedPlugins.TryGetValue(nameof(ITranslateModule), out var translateModule) || string.IsNullOrEmpty(translateModule))
        {
            results.Add(ValidateResult.Invalid(Resources.TranslateModule, """
                翻訳モジュールが選択されていません。
                「対象ごとの設定」→「全体設定」タブの「翻訳モジュール」を設定してください。
                """));
        }

        // キャッシュモジュールが選択されているかチェック
        if (!settings.SelectedPlugins.TryGetValue(nameof(ICacheModule), out var cacheModule) || string.IsNullOrEmpty(cacheModule))
        {
            results.Add(ValidateResult.Invalid(Resources.CacheModule, """
                キャッシュモジュールが選択されていません。
                「対象ごとの設定」→「全体設定」タブの「キャッシュモジュール」を設定してください。
                """));
        }

        // 各バリデーターによる検証
        foreach (var validator in validators)
        {
            var result = await validator.Validate(settings);
            if (!result.IsValid)
            {
                results.Add(result);
            }
        }

        return results;
    }
}
