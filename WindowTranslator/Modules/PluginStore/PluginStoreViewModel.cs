using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System.Windows;
using WindowTranslator.Properties;
using Wpf.Ui;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// プラグインストアのViewModel
/// </summary>
public partial class PluginStoreViewModel : ObservableObject, IDisposable
{
    private readonly NuGetPluginService nugetService;
    private readonly ILogger<PluginStoreViewModel> logger;
    private readonly IContentDialogService dialogService;
    private CancellationTokenSource? readmeLoadCancellation;
    private PluginPackageViewModel? selectedPackage;
    private PluginStoreSnapshot? pendingSnapshot;
    private PluginStoreSnapshot? appliedSnapshot;
    private bool disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    private bool hideDisclaimer;

    public bool HasError => this.ErrorMessage is not null;

    public PluginPackageViewModel? SelectedPackage
    {
        get => this.selectedPackage;
        set
        {
            var previous = this.selectedPackage;
            if (!SetProperty(ref this.selectedPackage, value))
            {
                return;
            }

            if (previous is not null)
            {
                previous.PropertyChanged -= OnSelectedPackagePropertyChanged;
                previous.IsReadmeLoading = false;
            }
            if (value is not null)
            {
                value.PropertyChanged += OnSelectedPackagePropertyChanged;
                StartReadmeLoad(value);
            }
            else
            {
                this.readmeLoadCancellation?.Cancel();
                this.readmeLoadCancellation = null;
            }
        }
    }

    public ObservableCollection<PluginPackageViewModel> Packages { get; } = [];

    public PluginStoreViewModel(
        NuGetPluginService nugetService,
        ILogger<PluginStoreViewModel> logger,
        IContentDialogService dialogService)
    {
        this.nugetService = nugetService;
        this.logger = logger;
        this.dialogService = dialogService;
        this.nugetService.PackageInformationUpdated += OnPackageInformationUpdated;
        ApplyPackageSnapshot(this.nugetService.PackageSnapshot);
    }

    private void OnPackageInformationUpdated(object? sender, EventArgs e)
    {
        if (this.disposed)
        {
            return;
        }

        var snapshot = this.nugetService.PackageSnapshot;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyPackageSnapshot(snapshot);
        }
        else
        {
            _ = dispatcher.BeginInvoke(() => ApplyPackageSnapshot(snapshot));
        }
    }

    private void ApplyPackageSnapshot(PluginStoreSnapshot snapshot)
    {
        if (this.disposed)
        {
            return;
        }
        if (ReferenceEquals(this.appliedSnapshot, snapshot))
        {
            return;
        }
        if (this.Packages.Any(package => package.IsInstalling))
        {
            this.pendingSnapshot = snapshot;
            return;
        }

        this.pendingSnapshot = null;
        this.appliedSnapshot = snapshot;
        var selectedPackageId = this.SelectedPackage?.Id;
        var prereleaseSelections = this.Packages.ToDictionary(
            package => package.Id,
            package => package.UsePrerelease,
            StringComparer.OrdinalIgnoreCase);
        var installedPackages = snapshot.InstalledPackages.ToDictionary(
            package => package.Id,
            StringComparer.OrdinalIgnoreCase);

        this.Packages.Clear();
        foreach (var packageInfo in snapshot.Packages)
        {
            installedPackages.TryGetValue(packageInfo.Id, out var installedPackage);
            var package = new PluginPackageViewModel(
                packageInfo,
                isInstalled: installedPackage is not null,
                installedVersion: installedPackage?.Version,
                isCompatible: installedPackage?.IsCompatible ?? true,
                hasCompatiblePackageVersion: true);
            if (package.HasPrereleaseVersion
                && prereleaseSelections.TryGetValue(package.Id, out var usePrerelease))
            {
                package.UsePrerelease = usePrerelease;
            }
            this.Packages.Add(package);
        }

        foreach (var installedPackage in snapshot.InstalledPackages)
        {
            if (this.Packages.Any(package => package.Id.Equals(
                    installedPackage.Id,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            this.Packages.Add(new PluginPackageViewModel(
                new NuGetPackageInfo(
                    Id: installedPackage.Id,
                    Title: installedPackage.Id,
                    Description: string.Empty,
                    Authors: string.Empty,
                    ProjectUrl: null,
                    LicenseUrl: null,
                    Versions: []),
                isInstalled: true,
                installedVersion: installedPackage.Version,
                isCompatible: installedPackage.IsCompatible,
                hasCompatiblePackageVersion: false));
        }

        this.SelectedPackage = selectedPackageId is null
            ? null
            : this.Packages.FirstOrDefault(package => package.Id.Equals(
                selectedPackageId,
                StringComparison.OrdinalIgnoreCase));
        this.ErrorMessage = snapshot.Error is null ? null : Resources.NuGetSearchFailed;
        this.logger.LogInformation(
            "バックグラウンド更新から{Count}件のプラグインパッケージを反映しました。",
            snapshot.Packages.Count);
    }

    private void ApplyPendingSnapshot()
    {
        if (this.pendingSnapshot is { } snapshot
            && !this.Packages.Any(package => package.IsInstalling))
        {
            ApplyPackageSnapshot(snapshot);
        }
    }

    /// <summary>
    /// プラグインをインストールまたは更新します。
    /// </summary>
    [RelayCommand]
    public async Task InstallAsync(
        PluginPackageViewModel package,
        CancellationToken cancellationToken = default)
    {
        var version = package.LatestVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        package.IsInstalling = true;
        try
        {
            this.logger.LogInformation("プラグインのインストール開始: {PackageId} {Version}", package.Id, version);
            var progress = new Progress<double>(v => package.InstallProgress = v);
            await this.nugetService.InstallPackageAsync(
                package.Id,
                version,
                progress,
                cancellationToken).ConfigureAwait(true);

            package.IsInstalled = true;
            package.InstalledVersion = version;
            package.IsCompatible = true;
            package.InstallProgress = 0;

            this.logger.LogInformation("プラグインのインストール完了: {PackageId}", package.Id);

            await ShowRestartDialogAsync(
                Resources.PluginInstallSuccess,
                cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // キャンセルは正常
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "プラグインのインストールに失敗しました: {PackageId}", package.Id);
            await this.dialogService.ShowAlertAsync(
                Resources.PluginInstallFailed,
                ex.Message,
                Resources.Close,
                cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            package.IsInstalling = false;
            ApplyPendingSnapshot();
        }
    }

    /// <summary>
    /// プラグインをアンインストールします。
    /// </summary>
    [RelayCommand]
    public async Task UninstallAsync(PluginPackageViewModel package)
    {
        var result = await this.dialogService.ShowSimpleDialogAsync(new()
        {
            Title = Resources.Uninstall,
            Content = string.Format(Resources.UninstallConfirm, package.Title),
            PrimaryButtonText = Resources.Uninstall,
            CloseButtonText = Resources.Cancel,
        }).ConfigureAwait(true);

        if (result != Wpf.Ui.Controls.ContentDialogResult.Primary)
            return;

        package.IsInstalling = true;
        try
        {
            await this.nugetService.UninstallPackageAsync(package.Id).ConfigureAwait(true);
            package.IsInstalled = false;
            package.InstalledVersion = null;
            package.IsCompatible = true;

            await ShowRestartDialogAsync(Resources.Uninstall).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "プラグインのアンインストールに失敗しました: {PackageId}", package.Id);
            await this.dialogService.ShowAlertAsync(
                Resources.Uninstall,
                ex.Message,
                Resources.Close).ConfigureAwait(true);
        }
        finally
        {
            package.IsInstalling = false;
            ApplyPendingSnapshot();
        }
    }

    private async Task ShowRestartDialogAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        var result = await this.dialogService.ShowSimpleDialogAsync(new()
        {
            Title = title,
            Content = Resources.RestartRequired,
            PrimaryButtonText = Resources.RestartNow,
            CloseButtonText = Resources.Close,
        }, cancellationToken).ConfigureAwait(true);
        if (result == Wpf.Ui.Controls.ContentDialogResult.Primary)
        {
            ApplicationRestart.Restart();
        }
    }

    private void OnSelectedPackagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is PluginPackageViewModel package
            && ReferenceEquals(package, this.SelectedPackage)
            && e.PropertyName == nameof(PluginPackageViewModel.LatestVersion))
        {
            StartReadmeLoad(package);
        }
    }

    private void StartReadmeLoad(PluginPackageViewModel package)
    {
        this.readmeLoadCancellation?.Cancel();
        this.readmeLoadCancellation = null;
        package.ReadmeMarkdown = null;

        var version = package.LatestVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            package.IsReadmeLoading = false;
            return;
        }

        var cancellationSource = new CancellationTokenSource();
        this.readmeLoadCancellation = cancellationSource;
        package.IsReadmeLoading = true;
        _ = LoadPackageReadmeAsync(package, version, cancellationSource);
    }

    private async Task LoadPackageReadmeAsync(
        PluginPackageViewModel package,
        string version,
        CancellationTokenSource cancellationSource)
    {
        try
        {
            var readme = await this.nugetService.GetPackageReadmeAsync(
                package.Id,
                version,
                cancellationSource.Token).ConfigureAwait(true);
            if (!cancellationSource.IsCancellationRequested
                && ReferenceEquals(package, this.SelectedPackage)
                && string.Equals(version, package.LatestVersion, StringComparison.OrdinalIgnoreCase))
            {
                package.ReadmeMarkdown = readme;
            }
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            // 選択変更によるキャンセルは正常
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(
                ex,
                "プラグインREADMEの取得に失敗しました: {PackageId} {Version}",
                package.Id,
                version);
        }
        finally
        {
            if (ReferenceEquals(this.readmeLoadCancellation, cancellationSource))
            {
                package.IsReadmeLoading = false;
                this.readmeLoadCancellation = null;
            }
            cancellationSource.Dispose();
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.nugetService.PackageInformationUpdated -= OnPackageInformationUpdated;
        this.readmeLoadCancellation?.Cancel();
        this.readmeLoadCancellation = null;
        GC.SuppressFinalize(this);
    }

}

/// <summary>
/// プラグインパッケージの表示モデル
/// </summary>
public partial class PluginPackageViewModel : ObservableObject
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string Authors { get; }
    public string? IconUrl { get; }
    public bool IsOfficial { get; }
    public string? ReleaseVersion { get; }
    public string? PrereleaseVersion { get; }
    public string? LatestVersion => this.UsePrerelease
        ? this.PrereleaseVersion
        : this.ReleaseVersion;
    public bool HasPrereleaseVersion => this.PrereleaseVersion is not null;
    public bool CanInstall => !this.IsInstalling && this.LatestVersion is not null;
    public bool RequiresReinstall => this.IsInstalled
        && !this.IsCompatible
        && this.hasCompatiblePackageVersion
        && this.LatestVersion is not null;
    public bool CanUpdate => this.IsUpdateAvailable || this.RequiresReinstall;
    public string? ProjectUrl { get; }
    public string? LicenseUrl { get; }
    public bool HasProjectUrl => this.ProjectUrl is not null;
    public bool HasLicenseUrl => this.LicenseUrl is not null;
    private readonly bool hasCompatiblePackageVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotInstalled))]
    [NotifyPropertyChangedFor(nameof(RequiresReinstall))]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool isInstalled;

    public bool IsNotInstalled => !this.IsInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? installedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool isUpdateAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(RequiresReinstall))]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool isCompatible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool isInstalling;

    [ObservableProperty]
    private double installProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadme))]
    private string? readmeMarkdown;

    [ObservableProperty]
    private bool isReadmeLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LatestVersion))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(RequiresReinstall))]
    [NotifyPropertyChangedFor(nameof(CanUpdate))]
    private bool usePrerelease;

    public bool HasReadme => !string.IsNullOrWhiteSpace(this.ReadmeMarkdown);

    public string StatusText
    {
        get
        {
            if (this.IsInstalled && !this.IsCompatible)
                return Resources.PluginIncompatible;
            if (this.IsUpdateAvailable
                && this.InstalledVersion is not null
                && this.LatestVersion is not null)
                return string.Format(Properties.Resources.UpdateAvailableVersion, this.InstalledVersion, this.LatestVersion);
            if (this.IsInstalled && this.InstalledVersion is not null)
                return string.Format(Properties.Resources.InstalledVersion, this.InstalledVersion);
            return string.Empty;
        }
    }

    public PluginPackageViewModel(
        NuGetPackageInfo info,
        bool isInstalled,
        string? installedVersion,
        bool isCompatible = true,
        bool hasCompatiblePackageVersion = true)
    {
        var versions = info.Versions
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(version => (Text: version, Parsed: ParseVersion(version)))
            .Where(version => version.Parsed is not null)
            .ToArray();

        this.Id = info.Id;
        this.Title = info.Title;
        this.Description = info.Description;
        this.Authors = info.Authors;
        this.IconUrl = info.IconUrl;
        this.IsOfficial = info.Authors
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("Freesia", StringComparer.OrdinalIgnoreCase);
        this.ReleaseVersion = versions
            .Where(version => !version.Parsed!.IsPrerelease)
            .OrderByDescending(version => version.Parsed)
            .Select(version => version.Text)
            .FirstOrDefault();
        this.PrereleaseVersion = versions
            .Where(version => version.Parsed!.IsPrerelease)
            .OrderByDescending(version => version.Parsed)
            .Select(version => version.Text)
            .FirstOrDefault();
        this.ProjectUrl = info.ProjectUrl;
        this.LicenseUrl = info.LicenseUrl;
        this.hasCompatiblePackageVersion = hasCompatiblePackageVersion;
        this.isInstalled = isInstalled;
        this.installedVersion = installedVersion;
        this.isCompatible = isCompatible;
        this.usePrerelease = this.PrereleaseVersion is not null
            && NuGetVersion.TryParse(installedVersion, out var installed)
            && installed.IsPrerelease;
        RefreshUpdateAvailable();
    }

    partial void OnIsInstalledChanged(bool value) => RefreshUpdateAvailable();

    partial void OnInstalledVersionChanged(string? value) => RefreshUpdateAvailable();

    partial void OnUsePrereleaseChanged(bool value) => RefreshUpdateAvailable();

    partial void OnIsCompatibleChanged(bool value) => RefreshUpdateAvailable();

    private void RefreshUpdateAvailable()
    {
        this.IsUpdateAvailable = this.IsInstalled
            && this.InstalledVersion is not null
            && this.LatestVersion is not null
            && IsNewerVersion(this.LatestVersion, this.InstalledVersion);
    }

    private static NuGetVersion? ParseVersion(string version)
        => NuGetVersion.TryParse(version, out var parsed) ? parsed : null;

    private static bool IsNewerVersion(string latestVersion, string installedVersion)
    {
        if (NuGetVersion.TryParse(latestVersion, out var latest)
            && NuGetVersion.TryParse(installedVersion, out var installed))
        {
            return latest > installed;
        }

        return string.Compare(latestVersion, installedVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }
}
