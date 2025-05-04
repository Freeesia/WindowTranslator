using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;

namespace WindowTranslator.Plugin.LLMPlugin;

public class LLMOptions : IPluginParam
{
    [DisplayName("認識補正")]
    [SelectorStyle(SelectorStyle.ComboBox)]
    public CorrectMode CorrectMode { get; set; }

    [DisplayName("使用するモデル")]
    public string? Model { get; set; } = "gpt-4o-mini";

    [DisplayName("APIキー")]
    [DataType(DataType.Password)]
    public string? ApiKey { get; set; }

    [DisplayName("接続先")]
    public string? Endpoint { get; set; }

    [Height(120)]
    [DisplayName("補正サンプル")]
    [DataType(DataType.MultilineText)]
    public string? CorrectSample { get; set; }

    [Height(120)]
    [DisplayName("翻訳時に利用する文脈情報")]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [DisplayName("用語集パス")]
    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "用語集 (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }
}

public enum CorrectMode
{
    None,
    Text,
    Image,
}
