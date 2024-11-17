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
    public GoogleAIModel Model { get; set; } = GoogleAIModel.Gemini15Flash;

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
}

public enum GoogleAIModel
{
    [Display(Name = "Gemini 1.5 Flash")]
    Gemini15Flash,

    [Display(Name = "Gemini 1.5 Pro")]
    Gemini15Pro,
}

public static class GoogleAIModelExtensions
{
    public static string GetName(this GoogleAIModel model) => model switch
    {
        GoogleAIModel.Gemini15Flash => GoogleAIModels.Gemini15Flash,
        GoogleAIModel.Gemini15Pro => GoogleAIModels.GeminiPro,
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