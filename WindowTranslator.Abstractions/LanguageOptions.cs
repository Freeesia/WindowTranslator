using System.Globalization;

namespace WindowTranslator;

public class LanguageOptions
{
    public string Source { get; set; } = "en-US";
    public string Target { get; set; } = CultureInfo.CurrentUICulture.Name;
}