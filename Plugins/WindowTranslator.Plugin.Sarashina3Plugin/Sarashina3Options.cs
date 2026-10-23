using System.ComponentModel.DataAnnotations;
using WindowTranslator.Modules;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.Sarashina3Plugin;

[DisplayName("Sarashina3")]
public sealed class Sarashina3Options : IPluginParam
{
    [DataType(DataType.Password)]
    public string? ApiKey { get; set; }
}

public sealed class Sarashina3Validator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        if (settings.SelectedPlugins.GetValueOrDefault(nameof(ITranslateModule)) != nameof(Sarashina3Translator))
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        var options = settings.PluginParams.GetValueOrDefault(nameof(Sarashina3Options)) as Sarashina3Options;
        if (string.IsNullOrWhiteSpace(options?.ApiKey))
        {
            return ValueTask.FromResult(ValidateResult.Invalid("Sarashina3", "Sarashina APIキーを設定してください。"));
        }

        return ValueTask.FromResult(
            !Sarashina3Translator.SupportsLanguage(settings.Language.Source)
            || !Sarashina3Translator.SupportsLanguage(settings.Language.Target)
                ? ValidateResult.Invalid("Sarashina3", "Sarashina Translate APIは日本語と英語の翻訳に対応しています。")
                : ValidateResult.Valid);
    }
}
