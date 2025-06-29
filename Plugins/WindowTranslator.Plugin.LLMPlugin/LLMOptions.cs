using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.LLMPlugin.Properties;

namespace WindowTranslator.Plugin.LLMPlugin;

public class LLMOptions : IPluginParam
{
    [SelectorStyle(SelectorStyle.ComboBox)]
    public CorrectMode CorrectMode { get; set; }

    public bool WaitCorrect { get; set; }

    public string? Model { get; set; } = "gpt-4o-mini";

    [DataType(DataType.Password)]
    public string? ApiKey { get; set; }

    [LocalizedDescription(typeof(Resources), $"{nameof(Endpoint)}_Desc")]
    public string? Endpoint { get; set; }

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? CorrectSample { get; set; }

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "CSV (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }
}

public enum CorrectMode
{
    [LocalizedDescription(typeof(Resources), $"{nameof(CorrectMode)}_{nameof(None)}")]
    None,
    [LocalizedDescription(typeof(Resources), $"{nameof(CorrectMode)}_{nameof(Text)}")]
    Text,
    [LocalizedDescription(typeof(Resources), $"{nameof(CorrectMode)}_{nameof(Image)}")]
    Image,
}

public class LLMOptionsValidator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        var op = settings.PluginParams.GetValueOrDefault(nameof(LLMOptions)) as LLMOptions;
        // APIキーが設定されている場合は有効
        if (!string.IsNullOrEmpty(op?.ApiKey))
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        // 翻訳モジュールでも補正も利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(LLMTranslator) && (op?.CorrectMode ?? CorrectMode.None) == CorrectMode.None)
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        return ValueTask.FromResult(ValidateResult.Invalid("LLM", """
            翻訳モジュールにLLMが選択もしくは認識補正が有効化されています。
            
            LLMの利用にはAPIキーが必要です。
            「対象ごとの設定」→「LLMOptions」タブのAPIキーを設定してください。

            ※ローカルLLMを利用する場合もライブラリの制約のためAPIキーが必要です。
            """));
    }
}