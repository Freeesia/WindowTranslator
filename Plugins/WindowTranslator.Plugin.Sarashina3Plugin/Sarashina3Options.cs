using System.ComponentModel.DataAnnotations;
using System.Globalization;
using PropertyTools.DataAnnotations;
using WindowTranslator.Modules;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.Sarashina3Plugin;

[DisplayName("Sarashina3")]
public sealed class Sarashina3Options : IPluginParam
{
    [DataType(DataType.Password)]
    public string? ApiKey { get; set; }

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }
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
        return ValueTask.FromResult(string.IsNullOrWhiteSpace(options?.ApiKey)
            ? ValidateResult.Invalid("Sarashina3", "Sarashina3 APIキーを設定してください。")
            : ValidateResult.Valid);
    }
}
