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
    public string Source { get; set; } = GetDefaultSourceLanguage();

    /// <summary>
    /// 翻訳先言語を取得または設定します。
    /// </summary>
    public string Target { get; set; } = CultureInfo.CurrentUICulture.Name;

    /// <summary>
    /// OSの言語設定に基づいてデフォルトのソース言語を取得します。
    /// </summary>
    /// <returns>
    /// OSの言語が英語の場合は日本語（ja-JP）、それ以外の場合は英語（en-US）
    /// </returns>
    private static string GetDefaultSourceLanguage()
    {
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        // OSの言語が英語の場合は日本語、それ以外は英語
        return currentCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "ja-JP" : "en-US";
    }
}

/// <summary>
/// 言語に関連するユーティリティメソッドを提供します。
/// </summary>
public static class LanguageUtility
{
    /// <summary>
    /// 指定された言語がスペースを含むかどうかを判断します。
    /// </summary>
    /// <param name="lang">言語コード</param>
    /// <returns>スペースを含む場合は true、それ以外の場合は false</returns>
    public static bool IsSpaceLang(string lang)
        => lang[..2] is not "ja" or "zh";

    /// <summary>
    /// 指定された言語が特殊なグリフの言語かどうかを判断します。
    /// </summary>
    /// <param name="lang">言語コード</param>
    /// <returns>特殊な言語の場合は true、それ以外の場合は false</returns>
    public static bool IsSpecialLang(string lang)
        => lang[..2] is "ja" or "zh" or "ko" or "ru";

    /// <summary>
    /// 指定されたテキストの単語数をカウントします。
    /// </summary>
    /// <param name="text">カウントするテキスト</param>
    /// <returns>単語数</returns>
    public static int WordCount(string text)
    {
        var span = text.AsSpan();
        var count = 0;
        while (!span.IsEmpty)
        {
            count++;
            var index = span.IndexOf(' ');
            if (index == -1)
            {
                break;
            }
            span = span[(index + 1)..];
        }
        return count;
    }
}