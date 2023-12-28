using System.Globalization;

namespace WindowTranslator;

/// <summary>
/// 翻訳言語のオプションを表します。
/// </summary>
public class LanguageOptions
{
    /// <summary>
    /// 翻訳元言語を取得または設定します。
    /// </summary>
    public string Source { get; set; } = "en-US";

    /// <summary>
    /// 翻訳先言語を取得または設定します。
    /// </summary>
    public string Target { get; set; } = CultureInfo.CurrentUICulture.Name;
}