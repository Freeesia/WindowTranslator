using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;

namespace WindowTranslator.Modules.Settings;

[OpenDialog]
sealed partial class AllSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IUpdateChecker updateChecker;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasUpdate;

    [ObservableProperty]
    private string? newVersion;

    public string Title { get; } = $"WindowTranslator {Assembly.GetExecutingAssembly().GetName().Version}";

    public AllSettingsViewModel([Inject] IUpdateChecker updateChecker)
    {
        this.updateChecker = updateChecker;
        this.updateChecker.UpdateAvailable += UpdateChecker_UpdateAvailable;
        SetUpUpdateInfo();
    }

    private void UpdateChecker_UpdateAvailable(object? sender, EventArgs e)
        => SetUpUpdateInfo();

    private void SetUpUpdateInfo()
    {
        if (this.updateChecker.HasUpdate)
        {
            this.NewVersion = this.updateChecker.LatestVersion;
            this.HasUpdate = true;
        }
    }

    [RelayCommand]
    public static void OpenReleaseNotes()
        => Process.Start(new ProcessStartInfo("https://github.com/Freeesia/WindowTranslator/releases/latest") { UseShellExecute = true });

    [RelayCommand]
    public Task CheckUpdateAsync(CancellationToken token)
        => this.updateChecker.CheckAsync(token);

    public void Dispose()
    {
        this.updateChecker.UpdateAvailable -= UpdateChecker_UpdateAvailable;
    }
}
