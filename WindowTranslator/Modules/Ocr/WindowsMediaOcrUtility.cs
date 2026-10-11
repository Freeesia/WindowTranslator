using System.Diagnostics;
using System.IO;

namespace WindowTranslator.Modules.Ocr;

public static class WindowsMediaOcrUtility
{
    public static string ConvertLanguage(string lang) => lang switch
    {
        "zh-Hant" => "zh-TW",
        "zh-Hans" => "zh-CN",
        _ => lang,
    };

    internal static bool ContainsCjk(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            // 仮名・漢字・ハングルと、補助平面の漢字を判定する
            if (rune.Value is >= 0x1100 and <= 0x11FF
                or >= 0x3040 and <= 0x9FFF
                or >= 0xA960 and <= 0xA97F
                or >= 0xAC00 and <= 0xD7FF
                or >= 0xF900 and <= 0xFAFF
                or >= 0x20000 and <= 0x2FA1F
                or >= 0x30000 and <= 0x323AF)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsInstalledLanguage(string lang)
        => Directory.Exists(@$"C:\Windows\OCR\{ConvertLanguage(lang)}");

    public static async Task InstallLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo("powershell.exe", $"-Command \"Install-Language -Language {ConvertLanguage(language)} -ExcludeFeatures -AsJob\"")
        {
            Verb = "runas", // 管理者権限で実行
            UseShellExecute = true,
            CreateNoWindow = true,
        };
        var p = Process.Start(info);
        p!.WaitForExit();
        while (!IsInstalledLanguage(language))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
    }
}
