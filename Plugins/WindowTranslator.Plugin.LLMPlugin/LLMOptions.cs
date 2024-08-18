using System.ComponentModel.DataAnnotations;

namespace WindowTranslator.Plugin.LLMPlugin;

public class LLMOptions : IPluginParam
{
    public bool IsEnabledCorrenct { get; set; }
    public string? Model { get; set; }
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }

    [DataType(DataType.MultilineText)]
    public string? CorrectSample { get; set; }

    [DataType(DataType.MultilineText)]
    public string? TranslateSample { get; set; }
}
