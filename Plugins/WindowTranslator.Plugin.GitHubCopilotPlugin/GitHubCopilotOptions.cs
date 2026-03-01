using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GitHubCopilotPlugin.Properties;

namespace WindowTranslator.Plugin.GitHubCopilotPlugin;

public class GitHubCopilotOptions : IPluginParam
{
    [LocalizedDescription(typeof(Resources), $"{nameof(Model)}_Desc")]
    public string Model { get; set; } = "gpt-4o";

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "CSV (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }
}

public class GitHubCopilotValidator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(GitHubCopilotTranslator))
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        // GitHub Copilot CLIがPATHに存在するか確認
        var cliPath = FindCliInPath("copilot");
        if (cliPath is not null)
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        return ValueTask.FromResult(ValidateResult.Invalid("GitHub Copilot", Resources.InvalidOptions));
    }

    private static string? FindCliInPath(string fileName)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
            var fullPathExe = Path.Combine(path, fileName + ".exe");
            if (File.Exists(fullPathExe))
            {
                return fullPathExe;
            }
        }
        return null;
    }
}
