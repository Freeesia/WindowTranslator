using System.ComponentModel.DataAnnotations;
using GenerativeAI;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GoogleAIPlugin.Properties;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public partial class GoogleAIOptions : IPluginParam
{
    [SelectorStyle(SelectorStyle.ComboBox)]
    public CorrectMode CorrectMode { get; set; }

    [SelectorStyle(SelectorStyle.ComboBox)]
    public GoogleAIModel Model { get; set; } = GoogleAIModel.Gemini15Flash;

    [LocalizedDescription(typeof(Resources), $"{nameof(PreviewModel)}_Desc")]
    public string? PreviewModel { get; set; }

    [DataType(DataType.Password)]
    public string? ApiKey { get; set; }

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

public enum GoogleAIModel
{
    Gemini15Flash,
    Gemini15Pro,
    Gemini20FlashLite,
    Gemini20Flash,
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

public static class GoogleAIModelExtensions
{
    public static string GetName(this GoogleAIModel model) => model switch
    {
        GoogleAIModel.Gemini15Flash => GoogleAIModels.Gemini15Flash,
        GoogleAIModel.Gemini15Pro => GoogleAIModels.Gemini15Pro,
        GoogleAIModel.Gemini20FlashLite => "models/gemini-2.0-flash-lite",
        GoogleAIModel.Gemini20Flash => "models/gemini-2.0-flash",
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };
}

public class GoogleAIValidator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        var op = settings.PluginParams.GetValueOrDefault(nameof(GoogleAIOptions)) as GoogleAIOptions;
        // APIキーが設定されている場合は有効
        if (!string.IsNullOrEmpty(op?.ApiKey))
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        // 翻訳モジュールでも補正も利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(GoogleAITranslator) && (op?.CorrectMode ?? CorrectMode.None) == CorrectMode.None)
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        return ValueTask.FromResult(ValidateResult.Invalid("Gemini", """
            翻訳モジュールに「Gemini翻訳」が選択もしくは認識補正が有効化されています。
            
            Geminiの利用にはAPIキーが必要です。
            「対象ごとの設定」→「Gemini設定」タブのAPIキーを設定してください。

            APIキーはGeminiの[APIキーページ](https://aistudio.google.com/app/apikey)から取得できます。
            """));
    }
}