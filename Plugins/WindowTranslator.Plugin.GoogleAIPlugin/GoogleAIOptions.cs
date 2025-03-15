using System.ComponentModel.DataAnnotations;
using GenerativeAI.Models;
using PropertyTools.DataAnnotations;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public class GoogleAIOptions : IPluginParam
{
    [DisplayName("認識補正を利用するか")]
    public bool IsEnabledCorrect { get; set; }

    [DisplayName("使用するモデル")]
    [SelectorStyle(SelectorStyle.ComboBox)]
    public GoogleAIModel Model { get; set; } = GoogleAIModel.Gemini15Flash;

    [DisplayName("使用するプレビューモデル")]
    [Description("モデル名を入力すると「使用するモデル」より優先されます")]
    public string? PreviewModel { get; set; }

    [DisplayName("APIキー")]
    [DataType(DataType.Password)]
    public string? ApiKey { get; set; }

    [Height(120)]
    [DataType(DataType.MultilineText)]
    [DisplayName("補正サンプル")]
    public string? CorrectSample { get; set; }

    [Height(120)]
    [DataType(DataType.MultilineText)]
    [DisplayName("翻訳時に利用する文脈情報")]
    public string? TranslateContext { get; set; }
    
    [DisplayName("用語集パス")]
    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "用語集 (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }
}

public enum GoogleAIModel
{
    [Display(Name = "Gemini 1.5 Flash (～2025年9月24日)")]
    Gemini15Flash,

    [Display(Name = "Gemini 1.5 Pro (～2025年9月24日)")]
    Gemini15Pro,

    [Display(Name = "Gemini 2.0 Flash Lite")]
    Gemini20FlashLite,

    [Display(Name = "Gemini 2.0 Flash")]
    Gemini20Flash,
}

public static class GoogleAIModelExtensions
{
    public static string GetName(this GoogleAIModel model) => model switch
    {
        GoogleAIModel.Gemini15Flash => GoogleAIModels.Gemini15Flash,
        GoogleAIModel.Gemini15Pro => GoogleAIModels.GeminiPro,
        GoogleAIModel.Gemini20FlashLite => "gemini-2.0-flash-lite",
        GoogleAIModel.Gemini20Flash => "gemini-2.0-flash",
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };
}

public class GoogleAIValidator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        var op = settings.PluginParams.GetValueOrDefault(nameof(GoogleAIOptions)) as GoogleAIOptions;
        // 翻訳モジュールでも補正も利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(GoogleAITranslator) && !(op?.IsEnabledCorrect ?? false))
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }
        // APIキーが設定されている場合は有効
        if (!string.IsNullOrEmpty(op?.ApiKey))
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        return ValueTask.FromResult(ValidateResult.Invalid("GoogleAI", """
            翻訳モジュールにGoogleAIが選択もしくは認識補正が有効化されています。
            
            GoogleAIの利用にはAPIキーが必要です。
            「対象ごとの設定」→「GoogleAIOptions」タブのAPIキーを設定してください。

            APIキーはGoogleAIの[APIキーページ](https://aistudio.google.com/app/apikey)から取得できます。
            """));
    }
}