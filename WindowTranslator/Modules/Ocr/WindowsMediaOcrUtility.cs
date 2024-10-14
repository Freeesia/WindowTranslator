using System.Collections;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using System.Diagnostics;

namespace WindowTranslator.Modules.Ocr;

public static class WindowsMediaOcrUtility
{
    public static string ConvertLanguage(string lang) => lang switch
    {
        "zh-Hant" => "zh-TW",
        "zh-Hans" => "zh-CN",
        _ => lang,
    };

    public static async Task<bool> IsInstalledLanguageAsync(string lang)
    {
        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        ps.AddScript($"Get-InstalledLanguage -language {ConvertLanguage(lang)}");
        var result = await ps.InvokeAsync().ConfigureAwait(false);
        var output = (IList)result.Single().BaseObject;
        if (output.Count == 0)
        {
            return false;
        }
        var langInfo = output[0];
        var features = langInfo!.GetType().GetField("LanguageFeatures")!.GetValue(langInfo);
        return ((int)features! & 0x20) == 0x20;
    }

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
        while (!await IsInstalledLanguageAsync(language).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
    }
}
