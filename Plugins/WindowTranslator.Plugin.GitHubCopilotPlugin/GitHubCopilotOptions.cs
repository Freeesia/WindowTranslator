using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Plugin.GitHubCopilotPlugin.Properties;

namespace WindowTranslator.Plugin.GitHubCopilotPlugin;

public class GitHubCopilotOptions : IPluginParam
{
    [LocalizedDescription(typeof(Resources), $"{nameof(Model)}_Desc")]
    public string Model { get; set; } = "gpt-5-mini";

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "CSV (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }
}
