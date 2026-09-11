using System.Reflection;
using System.Runtime.InteropServices;
using GitHub.Copilot;
using WindowTranslator.Plugin.GitHubCopilotPlugin.Properties;

namespace WindowTranslator.Plugin.GitHubCopilotPlugin;

public static class Utility
{
    internal static string AuthenticationRequiredMessage
        => Resources.ResourceManager.GetString("AuthenticationRequired", Resources.Culture) ?? string.Empty;

    internal static CopilotClient CreateClient()
        => new(new() { Connection = RuntimeConnection.ForStdio(GetBundledCliPath()) });

    internal static async Task EnsureAuthenticatedAsync(CopilotClient client)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await client.StartAsync(cancellation.Token).ConfigureAwait(false);
        var status = await client.GetAuthStatusAsync(cancellation.Token).ConfigureAwait(false);
        if (!status.IsAuthenticated)
        {
            throw new AppUserException(AuthenticationRequiredMessage);
        }
    }

    private static string? GetPortableRid()
    {
        string text;
        if (OperatingSystem.IsWindows())
        {
            text = "win";
        }
        else if (OperatingSystem.IsLinux())
        {
            text = "linux";
        }
        else
        {
            if (!OperatingSystem.IsMacOS())
            {
                return null;
            }

            text = "osx";
        }

        var text2 = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null,
        };
        if (text2 == null)
        {
            return null;
        }

        return text + "-" + text2;
    }

    public static string? GetBundledCliPath()
    {
        string text = (OperatingSystem.IsWindows() ? "copilot.exe" : "copilot");
        string text2 = GetPortableRid() ?? Path.GetFileName(RuntimeInformation.RuntimeIdentifier);
        var searchedPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "runtimes", text2, "native", text);
        if (!File.Exists(searchedPath))
        {
            return null;
        }

        return searchedPath;
    }
}
