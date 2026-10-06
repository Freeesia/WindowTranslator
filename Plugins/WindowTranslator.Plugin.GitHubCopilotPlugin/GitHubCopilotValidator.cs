using System.Diagnostics;
using GitHub.Copilot;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GitHubCopilotPlugin.Properties;

namespace WindowTranslator.Plugin.GitHubCopilotPlugin;

public class GitHubCopilotValidator : ITargetSettingsValidator
{
    private static readonly SemaphoreSlim AuthenticationLock = new(1, 1);

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        if (!settings.SelectedPlugins.TryGetValue(nameof(ITranslateModule), out var selectedModule)
            || selectedModule != nameof(GitHubCopilotTranslator))
        {
            return ValidateResult.Valid;
        }

        var cliPath = Utility.GetBundledCliPath();
        if (cliPath is null)
        {
            return ValidateResult.Invalid(Resources.GitHubCopilotTranslator, Resources.InvalidOptions);
        }

        try
        {
            await AuthenticationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (await IsAuthenticatedAsync(cliPath).ConfigureAwait(false))
                {
                    return ValidateResult.Valid;
                }

                await StartLoginAsync(cliPath).ConfigureAwait(false);

                return await IsAuthenticatedAsync(cliPath).ConfigureAwait(false)
                    ? ValidateResult.Valid
                    : ValidateResult.Invalid(Resources.GitHubCopilotTranslator, Resources.InvalidOptions);
            }
            finally
            {
                AuthenticationLock.Release();
            }
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid(
                Resources.GitHubCopilotTranslator,
                $"{Resources.InvalidOptions}{Environment.NewLine}{Environment.NewLine}{ex.Message}");
        }
    }

    private static async Task<bool> IsAuthenticatedAsync(string cliPath)
    {
        await using var client = new CopilotClient(new() { Connection = RuntimeConnection.ForStdio(cliPath) });
        await client.StartAsync().ConfigureAwait(false);
        return (await client.GetAuthStatusAsync().ConfigureAwait(false)).IsAuthenticated;
    }

    private static async Task StartLoginAsync(string cliPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        startInfo.ArgumentList.Add("login");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("GitHub Copilot CLI could not be started.");
        await process.WaitForExitAsync().ConfigureAwait(false);
    }
}

