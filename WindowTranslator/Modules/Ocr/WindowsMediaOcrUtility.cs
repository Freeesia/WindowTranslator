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
