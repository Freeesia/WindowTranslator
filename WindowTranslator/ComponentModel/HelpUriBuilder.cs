using System.Globalization;

namespace WindowTranslator.ComponentModel;

internal static class HelpUriBuilder
{
    public static string Build(string pageName, CultureInfo culture)
    {
        const string baseUrl = "https://wt.studiofreesia.com/";
        var languageSuffix = culture.Name switch
        {
            "ja-JP" or "ja" => "",
            "zh-Hans" or "zh-CN" => ".zh-cn",
            "zh-Hant" or "zh-TW" => ".zh-tw",
            _ => "." + culture.TwoLetterISOLanguageName.ToLowerInvariant(),
        };

        return $"{baseUrl}{pageName}{languageSuffix}";
    }
}
