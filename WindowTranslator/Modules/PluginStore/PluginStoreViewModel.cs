using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
    private readonly ISnackbarService snackbarService;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private PluginPackageViewModel? selectedPackage;

    public ObservableCollection<PluginPackageViewModel> Packages { get; } = [];

    public PluginStoreViewModel(
        NuGetPluginService nugetService,
        ILogger<PluginStoreViewModel> logger,
        IContentDialogService dialogService,
        ISnackbarService snackbarService)
    {
        this.nugetService = nugetService;
        this.logger = logger;
        this.dialogService = dialogService;
        this.snackbarService = snackbarService;
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

            var packages = await this.nugetService.SearchPackagesAsync(cancellationToken).ConfigureAwait(true);
            this.logger.LogInformation("NuGetから{Count}件のプラグインパッケージを取得しました。", packages.Count);

            this.Packages.Clear();
            foreach (var pkg in packages)
            {
                installedDict.TryGetValue(pkg.Id, out var installedInfo);
                var isInstalled = installedInfo is not null;
                var installedVersion = installedInfo?.Version;
                var isUpdateAvailable = isInstalled
                    && installedVersion is not null
                    && IsNewerVersion(pkg.Version, installedVersion);

                this.Packages.Add(new PluginPackageViewModel(pkg, isInstalled, installedVersion, isUpdateAvailable));
            }

            // インストール済みだがNuGetに見つからないパッケージも表示
            foreach (var inst in installed)
            {
                if (!this.Packages.Any(p => p.Id.Equals(inst.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    this.Packages.Add(new PluginPackageViewModel(
                        new NuGetPackageInfo(inst.Id, inst.Version, inst.Id, string.Empty, string.Empty, null, null),
                        isInstalled: true,
                        installedVersion: inst.Version,
                        isUpdateAvailable: false));
                }
            }
        }
        catch (OperationCanceledException)
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
    public async Task InstallAsync(PluginPackageViewModel package)
    {
        package.IsInstalling = true;
        try
        {
            this.logger.LogInformation("プラグインのインストール開始: {PackageId} {Version}", package.Id, package.LatestVersion);
            var progress = new Progress<double>(v => package.InstallProgress = v);
            await this.nugetService.InstallPackageAsync(package.Id, package.LatestVersion, progress).ConfigureAwait(true);

            package.IsInstalled = true;
            package.InstalledVersion = package.LatestVersion;
            package.IsUpdateAvailable = false;
            package.InstallProgress = 0;

            this.logger.LogInformation("プラグインのインストール完了: {PackageId}", package.Id);

            // 再起動が必要な旨を表示
            await this.dialogService.ShowSimpleDialogAsync(new()
            {
                Title = Resources.PluginInstallSuccess,
                Content = Resources.RestartRequired,
                CloseButtonText = Resources.Close,
            }).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // キャンセルは正常
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "プラグインのインストールに失敗しました: {PackageId}", package.Id);
            await this.dialogService.ShowAlertAsync(
                Resources.PluginInstallFailed,
                ex.Message,
                Resources.Close).ConfigureAwait(true);
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
            package.IsUpdateAvailable = false;

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

    private static bool IsNewerVersion(string latestVersion, string installedVersion)
    {
        try
        {
            return Version.Parse(latestVersion) > Version.Parse(installedVersion);
        }
        catch
        {
            return string.Compare(latestVersion, installedVersion, StringComparison.OrdinalIgnoreCase) > 0;
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
    public string LatestVersion { get; }
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
    private bool isInstalling;

    [ObservableProperty]
    private double installProgress;

    public string StatusText
    {
        get
        {
            if (this.IsUpdateAvailable && this.InstalledVersion is not null)
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
        bool isUpdateAvailable)
    {
        this.Id = info.Id;
        this.Title = info.Title;
        this.Description = info.Description;
        this.Authors = info.Authors;
        this.LatestVersion = info.Version;
        this.ProjectUrl = info.ProjectUrl;
        this.LicenseUrl = info.LicenseUrl;
        this.isInstalled = isInstalled;
        this.installedVersion = installedVersion;
        this.isUpdateAvailable = isUpdateAvailable;
    }
}
