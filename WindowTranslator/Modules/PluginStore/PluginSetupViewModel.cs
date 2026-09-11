using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.PluginStore;

internal sealed partial class PluginSetupViewModel : ObservableObject, IDisposable
{
    private readonly NuGetPluginService service;
    private readonly IConfiguration configuration;
    private readonly ILogger<PluginSetupViewModel> logger;
    private readonly List<InstalledPackageInfo> installedPackages = [];
    private bool disposed;
    private bool finishRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyPropertyChangedFor(nameof(CanFinish))]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(FinishCommand), nameof(ReloadCommand))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(ReloadCommand))]
    private bool isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    [NotifyPropertyChangedFor(nameof(PrimaryText))]
    [NotifyPropertyChangedFor(nameof(SecondaryText))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(ReloadCommand))]
    private bool hasStarted;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    private bool hasSearchError;

    [ObservableProperty]
    private IReadOnlyList<PluginSetupGroup> groups = [];

    public string this[string key] => Resources.ResourceManager.GetString(key, Resources.Culture) ?? string.Empty;
    public bool CanInstall => !this.IsBusy && !this.IsCompleted
        && (this.HasStarted || (!this.IsLoading && !this.HasSearchError));
    public bool CanFinish => !this.IsBusy && !this.IsCompleted;
    private bool CanReload => !this.IsBusy && !this.IsLoading && !this.HasStarted;
    public bool CanSelect => !this.HasStarted && !this.IsBusy;
    public string PrimaryText => this.HasStarted ? this["SetupRetry"] : Resources.Install;
    public string SecondaryText => this.HasStarted ? Resources.Exit : this["SetupSkip"];
    public bool IsCompleted { get; private set; }
    public bool RequiresRestart => this.installedPackages.Count > 0;
    public event EventHandler? Completed;

    public PluginSetupViewModel(
        NuGetPluginService service, IConfiguration configuration, ILogger<PluginSetupViewModel> logger)
    {
        this.service = service;
        this.configuration = configuration;
        this.logger = logger;
        this.service.PackageInformationUpdated += OnPackageInformationUpdated;
        ApplySnapshot();
    }

    private void OnPackageInformationUpdated(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(ApplySnapshot);
        }
        else
        {
            ApplySnapshot();
        }
    }

    private void ApplySnapshot()
    {
        if (this.disposed || this.HasStarted)
        {
            return;
        }

        var snapshot = this.service.PackageSnapshot;
        this.IsLoading = ReferenceEquals(snapshot, PluginStoreSnapshot.Empty);
        var previousSelections = this.Groups.SelectMany(group => group.Packages)
            .ToDictionary(package => package.Package.Id, StringComparer.OrdinalIgnoreCase);
        var packages = snapshot.Packages.Where(package => package.IsOfficial)
            .Select(info => new PluginSetupPackage(info, this.configuration))
            .ToArray();
        foreach (var package in packages)
        {
            if (previousSelections.TryGetValue(package.Package.Id, out var previous))
            {
                package.IsSelected = previous.IsSelected;
                package.Package.UsePrerelease = previous.Package.UsePrerelease;
            }
        }
        this.Groups = [.. packages.GroupBy(package => package.Category)
            .OrderBy(group => group.Key)
            .Select(group => new PluginSetupGroup(group.Key, [.. group.OrderBy(package => package.Package.Title)]))];
        this.HasSearchError = snapshot.Error is not null;
        this.ErrorMessage = this.HasSearchError ? Resources.NuGetSearchFailed
            : !this.IsLoading && packages.Length == 0 ? this["SetupNoPackages"] : null;
    }

    [RelayCommand(CanExecute = nameof(CanReload))]
    private async Task ReloadAsync()
    {
        if (!this.CanReload)
        {
            return;
        }
        this.IsLoading = true;
        try
        {
            await this.service.RefreshPackageInformationAsync();
        }
        finally
        {
            this.IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    public async Task InstallAsync()
    {
        if (!this.CanInstall || this.IsCompleted)
        {
            return;
        }
        if (this.finishRequested)
        {
            await FinishAsync();
            return;
        }
        if (this.Groups.SelectMany(group => group.Packages)
            .Any(package => package.IsSelected && package.Package.LatestVersion is null))
        {
            this.ErrorMessage = this["SetupSelectVersion"];
            return;
        }
        this.HasStarted = true;
        this.IsBusy = true;
        this.HasSearchError = false;
        this.ErrorMessage = null;
        var failed = false;
        try
        {
            foreach (var package in this.Groups.SelectMany(group => group.Packages)
                .Where(package => package.IsSelected && !package.Package.IsInstalled))
            {
                package.ErrorMessage = null;
                package.Package.IsInstalling = true;
                package.Package.InstallProgress = 0;
                try
                {
                    var version = package.Package.LatestVersion
                        ?? throw new InvalidOperationException(this["SetupSelectVersion"]);
                    var installed = await this.service.InstallSetupPackageAsync(
                        package.Package.Id, version,
                        new Progress<double>(value => package.Package.InstallProgress = value));
                    this.installedPackages.Add(installed);
                    package.Package.IsInstalled = true;
                    package.Package.InstalledVersion = version;
                }
                catch (Exception ex)
                {
                    failed = true;
                    package.ErrorMessage = ex.Message;
                    this.logger.LogError(ex, "初回セットアップでインストールに失敗しました: {PackageId}", package.Package.Id);
                }
                finally
                {
                    package.Package.IsInstalling = false;
                }
            }
            if (failed)
            {
                this.ErrorMessage = this["SetupInstallFailed"];
            }
            else
            {
                await SaveAsync();
            }
        }
        finally
        {
            this.IsBusy = false;
        }
        if (this.IsCompleted)
        {
            this.Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    // スキップ時は未選択扱いで完了し、失敗後の終了時は成功済みの一覧を保存する。
    [RelayCommand(CanExecute = nameof(CanFinish))]
    public async Task FinishAsync()
    {
        if (!this.CanFinish || this.IsCompleted)
        {
            return;
        }
        this.finishRequested = true;
        this.HasStarted = true;
        this.IsBusy = true;
        try
        {
            await SaveAsync();
        }
        finally
        {
            this.IsBusy = false;
        }
        if (this.IsCompleted)
        {
            this.Completed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task SaveAsync()
    {
        this.ErrorMessage = null;
        try
        {
            await this.service.CompleteSetupAsync(this.installedPackages);
            this.IsCompleted = true;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "初回セットアップの結果を保存できませんでした。");
            this.ErrorMessage = this["SetupSaveFailed"];
        }
    }

    public void Dispose()
    {
        this.disposed = true;
        this.service.PackageInformationUpdated -= OnPackageInformationUpdated;
        this.Completed = null;
    }
}

internal sealed record PluginSetupGroup(string Name, IReadOnlyList<PluginSetupPackage> Packages);

internal sealed partial class PluginSetupPackage : ObservableObject
{
    public PluginPackageViewModel Package { get; }
    public string Category { get; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private string? errorMessage;

    public PluginSetupPackage(NuGetPackageInfo info, IConfiguration configuration)
    {
        this.Package = new(info, false, null);
        var (categoryKey, modules) = GetDefinition(info.Id);
        this.Category = Resources.ResourceManager.GetString(categoryKey, Resources.Culture) ?? string.Empty;
        var targets = configuration.GetSection(nameof(UserSettings.Targets)).GetChildren().ToArray();
        this.isSelected = targets.SelectMany(target => target.GetSection(nameof(TargetSettings.SelectedPlugins)).GetChildren())
            .Any(selection => modules.Contains(selection.Value, StringComparer.OrdinalIgnoreCase));

        // 補正だけに使用しているプラグインも、保存された有効設定から移行対象にする。
        var optionsName = info.Id.Equals("WindowTranslator.Plugin.LLMPlugin", StringComparison.OrdinalIgnoreCase)
            ? "LLMOptions" : info.Id.Equals("WindowTranslator.Plugin.GoogleAIPlugin", StringComparison.OrdinalIgnoreCase)
                ? "GoogleAIOptions" : null;
        if (optionsName is not null)
        {
            this.isSelected |= targets.Any(target => target[$"PluginParams:{optionsName}:CorrectMode"] is { } mode
                && (mode.Equals("Text", StringComparison.OrdinalIgnoreCase)
                    || mode.Equals("Image", StringComparison.OrdinalIgnoreCase) || mode is "1" or "2"));
        }
        if (info.Id.Equals("WindowTranslator.Plugin.FoMPlugin", StringComparison.OrdinalIgnoreCase))
        {
            this.isSelected |= targets.Any(target => target.Key.Equals("FieldsOfMistria", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(target["PluginParams:FoMOptions:IsEnabledCorrect"], "false", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static (string CategoryKey, string[] Modules) GetDefinition(string packageId)
        => packageId.ToUpperInvariant() switch
        {
            "WINDOWTRANSLATOR.PLUGIN.ONEOCRPLUGIN" => ("OcrModule", ["OneOcr"]),
            "WINDOWTRANSLATOR.PLUGIN.TESSERACTOCRPLUGIN" => ("OcrModule", ["TesseractOcr"]),
            "WINDOWTRANSLATOR.PLUGIN.BERGAMOTTRANSLATORPLUGIN" => ("TranslateModule", ["BergamotTranslator"]),
            "WINDOWTRANSLATOR.PLUGIN.DEEPLTRANSLATEPLUGIN" => ("TranslateModule", ["DeepLTranslator"]),
            "WINDOWTRANSLATOR.PLUGIN.GITHUBCOPILOTPLUGIN" => ("TranslateModule", ["GitHubCopilotTranslator"]),
            "WINDOWTRANSLATOR.PLUGIN.GOOGLEAPPSSCTIPTPLUGIN" => ("TranslateModule", ["GasTranslator"]),
            "WINDOWTRANSLATOR.PLUGIN.PLAMOPLUGIN" => ("TranslateModule", ["PLaMoTranslator"]),
            "WINDOWTRANSLATOR.PLUGIN.LLMPLUGIN" => ("SetupTranslationOcr", ["LLMTranslator", "LLMOcr"]),
            "WINDOWTRANSLATOR.PLUGIN.GOOGLEAIPLUGIN" => ("SetupTranslationOcr", ["GoogleAITranslator", "GoogleAIOcr"]),
            "WINDOWTRANSLATOR.PLUGIN.FOMPLUGIN" => ("SetupFilters", ["FoMFilterModule"]),
            "WINDOWTRANSLATOR.PLUGIN.COLORTHIEFPLUGIN" => ("SetupColors", ["ColorThiefModule"]),
            _ => ("SetupOther", []),
        };
}
