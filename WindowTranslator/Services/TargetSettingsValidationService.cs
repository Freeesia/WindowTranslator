using Microsoft.Extensions.DependencyInjection;
using WindowTranslator.Properties;

namespace WindowTranslator.Services;

/// <summary>
/// 翻訳対象設定の検証を行うサービス
/// </summary>
public interface ITargetSettingsValidationService
{
    /// <summary>
    /// 設定の検証を行う
    /// </summary>
    /// <param name="targetName">対象名</param>
    /// <param name="settings">検証する設定</param>
    /// <returns>検証結果のリスト（空の場合は全て有効）</returns>
    Task<IReadOnlyList<ValidateResult>> ValidateAsync(string targetName, TargetSettings settings);
}

/// <summary>
/// 翻訳対象設定の検証を行うサービス実装
/// </summary>
public class TargetSettingsValidationService(IServiceProvider serviceProvider) : ITargetSettingsValidationService
{
    private readonly IServiceProvider serviceProvider = serviceProvider;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ValidateResult>> ValidateAsync(string targetName, TargetSettings settings)
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
        var validators = serviceProvider.GetServices<ITargetSettingsValidator>();
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
