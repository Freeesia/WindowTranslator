using System.ComponentModel.DataAnnotations;
using GenerativeAI.Models;
using PropertyTools.DataAnnotations;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public class GoogleAIOptions : IPluginParam
{
    [DisplayName("認識補正を利用するか")]
    public bool IsEnabledCorrect { get; set; }

    [DisplayName("使用するモデル")]
    public GoogleAIModel Model { get; set; } = GoogleAIModel.Gemini15Flash;

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
