using System.ComponentModel.DataAnnotations;
using GenerativeAI.Models;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public class GoogleAIOptions : IPluginParam
{
    public bool IsEnabledCorrenct { get; set; }
    public GoogleAIModel Model { get; set; } = GoogleAIModel.Gemini15Flash;
    public string? ApiKey { get; set; }

    [DataType(DataType.MultilineText)]
    public string? CorrectSample { get; set; }
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
