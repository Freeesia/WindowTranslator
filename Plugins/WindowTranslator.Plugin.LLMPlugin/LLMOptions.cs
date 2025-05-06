using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Plugin.LLMPlugin.Properties;

namespace WindowTranslator.Plugin.LLMPlugin;

public class LLMOptions : IPluginParam
{
    [LocalizedDisplayName(typeof(Resources), nameof(CorrectMode))]
    [SelectorStyle(SelectorStyle.ComboBox)]
    public CorrectMode CorrectMode { get; set; }

    [LocalizedDisplayName(typeof(Resources), nameof(Model))]
    public string? Model { get; set; } = "gpt-4o-mini";

    [LocalizedDisplayName(typeof(Resources), nameof(ApiKey))]
    [DataType(DataType.Password)]
    public string? ApiKey { get; set; }

    [LocalizedDisplayName(typeof(Resources), nameof(Endpoint))]
    public string? Endpoint { get; set; }

    [Height(120)]
    [LocalizedDisplayName(typeof(Resources), nameof(CorrectSample))]
    [DataType(DataType.MultilineText)]
    public string? CorrectSample { get; set; }

    [Height(120)]
    [LocalizedDisplayName(typeof(Resources), nameof(TranslateContext))]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [LocalizedDisplayName(typeof(Resources), nameof(GlossaryPath))]
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
