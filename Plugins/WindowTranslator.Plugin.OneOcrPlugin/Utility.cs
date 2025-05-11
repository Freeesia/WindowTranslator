using System.Diagnostics;

namespace WindowTranslator.Plugin.OneOcrPlugin;

public static class Utility
{
    public static string? FindOneOcrPath()
    {
        var info = new ProcessStartInfo("powershell.exe", $"-Command \"(Get-AppxPackage -Name Microsoft.ScreenSketch).InstallLocation\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        using var p = Process.Start(info);
        p!.WaitForExit();
        if (p.ExitCode != 0)
        {
            return null;
        }
        var path = p.StandardOutput.ReadToEnd();
        return Path.Combine(path.Trim(), "SnippingTool");
    }
}