using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using WindowTranslator.Properties;
using Wpf.Ui;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// プラグインストアのViewModel
/// </summary>
public partial class PluginStoreViewModel : ObservableObject
{
    private readonly NuGetPluginService nugetService;
    private readonly ILogger<PluginStoreViewModel> logger;
    private readonly IContentDialogService dialogService;
    private CancellationTokenSource? readmeLoadCancellation;
    private PluginPackageViewModel? selectedPackage;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

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
    }

    /// <summary>
    /// プラグイン一覧を読み込みます。
    /// </summary>
    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (this.IsLoading)
            return;

        this.IsLoading = true;
        this.ErrorMessage = null;

        try
        {
            var installed = await this.nugetService.GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(true);
            var installedDict = installed.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);

            this.Packages.Clear();
            foreach (var inst in installed)
            {
                this.Packages.Add(new PluginPackageViewModel(
                    new NuGetPackageInfo(inst.Id, inst.Version, inst.Id, string.Empty, string.Empty, null, null),
                    isInstalled: true,
                    installedVersion: inst.Version));
            }

            var packages = await this.nugetService.SearchPackagesAsync(cancellationToken).ConfigureAwait(true);
            this.logger.LogInformation("NuGetから{Count}件のプラグインパッケージを取得しました。", packages.Count);

            this.Packages.Clear();
            foreach (var pkg in packages)
            {
                installedDict.TryGetValue(pkg.Id, out var installedInfo);
                var isInstalled = installedInfo is not null;
                var installedVersion = installedInfo?.Version;
                this.Packages.Add(new PluginPackageViewModel(pkg, isInstalled, installedVersion));
            }

            // インストール済みだがNuGetに見つからないパッケージも表示
            foreach (var inst in installed)
            {
                if (!this.Packages.Any(p => p.Id.Equals(inst.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    this.Packages.Add(new PluginPackageViewModel(
                        new NuGetPackageInfo(inst.Id, inst.Version, inst.Id, string.Empty, string.Empty, null, null),
                        isInstalled: true,
                        installedVersion: inst.Version));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // キャンセルは正常
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "NuGet検索に失敗しました。");
            this.ErrorMessage = Resources.NuGetSearchFailed;
        }
        finally
        {
            this.IsLoading = false;
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
            package.InstallProgress = 0;

            this.logger.LogInformation("プラグインのインストール完了: {PackageId}", package.Id);

            // 再起動が必要な旨を表示
            await this.dialogService.ShowSimpleDialogAsync(new()
            {
                Title = Resources.PluginInstallSuccess,
                Content = Resources.RestartRequired,
                CloseButtonText = Resources.Close,
            }, cancellationToken).ConfigureAwait(true);
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

        try
        {
            await this.nugetService.UninstallPackageAsync(package.Id).ConfigureAwait(true);
            package.IsInstalled = false;
            package.InstalledVersion = null;

            await this.dialogService.ShowSimpleDialogAsync(new()
            {
                Title = Resources.Uninstall,
                Content = Resources.RestartRequired,
                CloseButtonText = Resources.Close,
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "プラグインのアンインストールに失敗しました: {PackageId}", package.Id);
            await this.dialogService.ShowAlertAsync(
                Resources.Uninstall,
                ex.Message,
                Resources.Close).ConfigureAwait(true);
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
    public string? ReleaseVersion { get; }
    public string? PrereleaseVersion { get; }
    public string? LatestVersion => this.UsePrerelease
        ? this.PrereleaseVersion
        : this.ReleaseVersion;
    public bool HasPrereleaseVersion => this.PrereleaseVersion is not null;
    public bool CanInstall => !this.IsInstalling && this.LatestVersion is not null;
    public string? ProjectUrl { get; }
    public string? LicenseUrl { get; }

    [ObservableProperty]
    private bool isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? installedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isUpdateAvailable;

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
    private bool usePrerelease;

    public bool HasReadme => !string.IsNullOrWhiteSpace(this.ReadmeMarkdown);

    public string StatusText
    {
        get
        {
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
        string? installedVersion)
    {
        var versions = new[] { info.Version }
            .Concat(info.Versions ?? [])
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(version => (Text: version, Parsed: ParseVersion(version)))
            .Where(version => version.Parsed is not null)
            .ToArray();

        this.Id = info.Id;
        this.Title = info.Title;
        this.Description = info.Description;
        this.Authors = info.Authors;
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
        this.isInstalled = isInstalled;
        this.installedVersion = installedVersion;
        this.usePrerelease = this.PrereleaseVersion is not null
            && NuGetVersion.TryParse(installedVersion, out var installed)
            && installed.IsPrerelease;
        RefreshUpdateAvailable();
    }

    partial void OnIsInstalledChanged(bool value) => RefreshUpdateAvailable();

    partial void OnInstalledVersionChanged(string? value) => RefreshUpdateAvailable();

    partial void OnUsePrereleaseChanged(bool value) => RefreshUpdateAvailable();

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
