using System.Diagnostics;

namespace WindowTranslator.Plugin.OneOcrPlugin;

static class Utility
{
    private const string OneOcrDll = "oneocr.dll";
    public const string OneOcrModel = "oneocr.onemodel";
    private const string OnnxRuntimeDll = "onnxruntime.dll";
    public static string OneOcrPath { get; } = Path.Combine(PathUtility.UserDir, "OneOcr");

    private static async ValueTask<string?> GetInstallLocation(string appName)
    {
        var info = new ProcessStartInfo("powershell.exe", $"-Command \"(Get-AppxPackage -Name {appName}).InstallLocation\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        using var p = Process.Start(info);
        await p!.WaitForExitAsync().ConfigureAwait(false);
        if (p.ExitCode != 0)
        {
            return null;
        }
        var path = await p.StandardOutput.ReadToEndAsync();
        return path.Trim();
    }

    public static async ValueTask<string?> FindOneOcrPath()
    {
        var scketch = await GetInstallLocation("Microsoft.ScreenSketch").ConfigureAwait(false);
        if (!string.IsNullOrEmpty(scketch))
        {
            return Path.Combine(scketch, "SnippingTool");
        }
        return await GetInstallLocation("Microsoft.Photos").ConfigureAwait(false);
    }

    public static bool NeedCopyDll()
    {
        if (!File.Exists(Path.Combine(OneOcrPath, OneOcrDll)))
        {
            return true;
        }
        if (!File.Exists(Path.Combine(OneOcrPath, OneOcrModel)))
        {
            return true;
        }
        if (!File.Exists(Path.Combine(OneOcrPath, OnnxRuntimeDll)))
        {
            return true;
        }
        return false;
    }

    public static void CopyDll(string path)
    {
        Directory.CreateDirectory(OneOcrPath);
        File.Copy(Path.Combine(path, OneOcrDll), Path.Combine(OneOcrPath, OneOcrDll), true);
        File.Copy(Path.Combine(path, OneOcrModel), Path.Combine(OneOcrPath, OneOcrModel), true);
        File.Copy(Path.Combine(path, OnnxRuntimeDll), Path.Combine(OneOcrPath, OnnxRuntimeDll), true);
    }
}