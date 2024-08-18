using System.Collections;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using System.Diagnostics;
using System.Windows;

namespace WindowTranslator.Modules.LanguagePackInstaller;
public static class LanguagePackUtility
{
    public static bool IsInstalledLanguage(string lang)
    {
        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        ps.AddScript($"Get-InstalledLanguage -language {lang}");
        var output = (IList)ps.Invoke().Single().BaseObject;
        if (output.Count == 0)
        {
            return false;
        }
        var langInfo = output[0];
        var features = langInfo!.GetType().GetField("LanguageFeatures")!.GetValue(langInfo);
        return (((int)features!) & 0x20) == 0x20;
    }

    public static async Task InstallLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo("powershell.exe", $"-Command \"Install-Language -Language {language} -ExcludeFeatures -AsJob\"")
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

    public static bool InstallLanguageWithDialog(string language)
    {
        var dialog = new LanguagePackInstallDialog(language);
        dialog.Owner = Application.Current.MainWindow;
        return dialog.ShowDialog() ?? false;
    }
}
