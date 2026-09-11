using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Input;
using WindowTranslator.ComponentModel;
using WindowTranslator.Plugin.GitHubCopilotPlugin.Properties;

namespace WindowTranslator.Plugin.GitHubCopilotPlugin;

public partial class GitHubCopilotOptions : IPluginParam
{
    [property: Display(Order = -1)]
    [property: JsonIgnore]
    [RelayCommand]
    private async Task LoginAsync()
    {
        var path = Utility.GetBundledCliPath() ?? throw new AppUserException(Resources.InvalidOptions);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            ArgumentList = { "login" },
            UseShellExecute = true,
        }) ?? throw new AppUserException(Utility.AuthenticationRequiredMessage);
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new AppUserException(Utility.AuthenticationRequiredMessage);
        }

        await using var client = Utility.CreateClient();
        await Utility.EnsureAuthenticatedAsync(client);
    }

    [EditableItemsSource]
    [LocalizedDescription(typeof(Resources), $"{nameof(Model)}_Desc")]
    public string Model { get; set; } = "gpt-5-mini";

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "CSV (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }
}
