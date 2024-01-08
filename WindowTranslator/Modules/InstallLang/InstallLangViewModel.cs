using CommunityToolkit.Mvvm.Input;
using Microsoft.PowerShell;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using Kamishibai;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Windows;
using System.Security.Principal;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowTranslator.Modules.InstallLang;

[OpenDialog]
internal partial class InstallLangViewModel : ObservableObject
{
    private readonly string lang;
    private readonly IPresentationService presentationService;
    [ObservableProperty]
    private double progressValue;

    public string DispLang { get; }
    public bool IsAdmin { get; } = IsUserAdministrator();

    public InstallLangViewModel([Inject] IPresentationService presentationService, [Inject] IOptionsSnapshot<LanguageOptions> options)
    {
        this.lang = options.Value.Source;
        this.presentationService = presentationService;
        this.DispLang = CultureInfo.GetCultureInfo(options.Value.Source).DisplayName;
    }

    [RelayCommand]
    private async Task InstallLangAsync(Window window)
    {
        var defaultSessionState = InitialSessionState.CreateDefault();
        defaultSessionState.ExecutionPolicy = ExecutionPolicy.RemoteSigned;
        using var runspace = RunspaceFactory.CreateRunspace(defaultSessionState);
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        ps.AddScript($"Get-WindowsCapability -Online | Where-Object {{ $_.Name -Like 'Language.OCR*{lang}*' }} | Add-WindowsCapability -Online");
        ps.Streams.Progress.DataAdded += (s, e) =>
        {
            var progress = ps.Streams.Progress[e.Index];
            this.ProgressValue = progress.PercentComplete;
        };
        await ps.InvokeAsync();
        if (ps.HadErrors)
        {
            this.presentationService.ShowMessage("OCR言語パックインストール失敗。", icon: Kamishibai.MessageBoxImage.Warning, owner: window);
            Process.Start(new ProcessStartInfo("cmd.exe", "/c start \"\" ms-settings:regionlanguage-adddisplaylanguage") { CreateNoWindow = true });
        }
        else
        {
            this.presentationService.ShowMessage("OCR言語パックのインストールが完了しました。", icon: Kamishibai.MessageBoxImage.Information, owner: window);
        }
        // Window指定しないとアクティブじゃないときに閉じれない
        await this.presentationService.CloseDialogAsync(ps.HadErrors, window);
    }

    [RelayCommand]
    private static void RestartAsAdmin()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = $"/c start \"\" \"{Environment.ProcessPath}\"",
            Verb = "runas",
            UseShellExecute = true,
        };
        Process.Start(psi);
        Application.Current.Shutdown();
    }

    private static bool IsUserAdministrator()
    {
        bool isAdmin;
        try
        {
            var user = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(user);
            isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (UnauthorizedAccessException)
        {
            isAdmin = false;
        }
        catch (Exception)
        {
            isAdmin = false;
        }
        return isAdmin;
    }
}
