using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GitHubCopilotPlugin;

public class GitHubCopilotValidator : ITargetSettingsValidator
{
    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        if (settings.SelectedPlugins.GetValueOrDefault(nameof(ITranslateModule)) != nameof(GitHubCopilotTranslator))
        {
            return ValidateResult.Valid;
        }

        try
        {
            await using var client = Utility.CreateClient();
            await Utility.EnsureAuthenticatedAsync(client).ConfigureAwait(false);
            return ValidateResult.Valid;
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid("GitHub Copilot", ex.Message);
        }
    }
}
