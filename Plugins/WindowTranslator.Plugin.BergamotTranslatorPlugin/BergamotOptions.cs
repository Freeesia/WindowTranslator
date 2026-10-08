using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Plugin.BergamotTranslatorPlugin;

public class BergamotOptions : IPluginParam
{
    [Category("Bergamot|")]
    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "CSV (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }
}
