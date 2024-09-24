using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Modules.Settings;

[OpenDialog]
sealed partial class AllSettingsViewModel : ObservableObject, IDisposable
{
    private readonly IUpdateChecker updateChecker;
    private readonly IContentDialogService dialogService;
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasUpdate;

    [ObservableProperty]
    private string? newVersion;

    [ObservableProperty]
    private bool isStartup;

    public string Title { get; } = $"WindowTranslator {Assembly.GetExecutingAssembly().GetName().Version}";

    public AllSettingsViewModel([Inject] IUpdateChecker updateChecker, [Inject] IContentDialogService dialogService)
    {
        this.updateChecker = updateChecker;
        this.dialogService = dialogService;
        this.updateChecker.UpdateAvailable += UpdateChecker_UpdateAvailable;
        SetUpUpdateInfo();
        this.isStartup = GetIsStartup();
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

    async partial void OnIsStartupChanged(bool value)
    {
        var exe = Assembly.GetExecutingAssembly();
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new InvalidOperationException();
        var name = exe.GetName().Name ?? throw new InvalidOperationException();
        var path = Environment.ProcessPath ?? throw new InvalidOperationException();
        if (value)
        {
            key.SetValue(name, path);
            await this.dialogService.ShowAlertAsync("自動起動", $"{name}を自動起動に登録しました。", "OK");
        }
        else
        {
            key.DeleteValue(name, false);
            await this.dialogService.ShowAlertAsync("自動起動", $"{name}の自動起動を解除しました。", "OK");
        }
    }

    private static bool GetIsStartup()
    {
        var exe = Assembly.GetExecutingAssembly();
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
        string? name = exe.GetName().Name;
        return key?.GetValue(name) is { };
    }

    public void Dispose()
    {
        this.updateChecker.UpdateAvailable -= UpdateChecker_UpdateAvailable;
    }
}
