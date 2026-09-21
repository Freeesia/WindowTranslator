using System.ComponentModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WindowTranslator.ComponentModel;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.PluginStore;

internal sealed partial class PluginSetupViewModel : ObservableObject, IDisposable
{
    private readonly NuGetPluginService service;
    private readonly IConfiguration configuration;
    private readonly ILogger<PluginSetupViewModel> logger;
    private readonly Dispatcher? dispatcher;
    // 配布時に IsPublishable=true のプラグイン。開発用出力に DLL がなくても追加候補にはしない。
    private static readonly HashSet<string> BundledPackageIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowTranslator.Plugin.BergamotTranslatorPlugin",
        "WindowTranslator.Plugin.OneOcrPlugin",
        "WindowTranslator.Plugin.ColorThiefPlugin",
        "WindowTranslator.Plugin.OrcaRouterPlugin",
    };
    private readonly Dictionary<PluginSetupPackage, CancellationTokenSource> readmeLoads = [];
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
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    private bool isInstalling;

    [ObservableProperty]
    private double installProgress;

    public string this[string key] => Resources.ResourceManager.GetString(key, Resources.Culture) ?? string.Empty;
    public bool CanInstall => !this.IsBusy && !this.IsCompleted
        && (this.HasStarted || (!this.IsLoading && !this.HasSearchError));
    public bool CanFinish => !this.IsBusy && !this.IsCompleted;
    private bool CanReload => !this.IsBusy && !this.IsLoading && !this.HasStarted;
    public bool CanSelect => !this.HasStarted && !this.IsBusy;
    public string PrimaryText => this.HasStarted ? this["SetupRetry"] : Resources.Install;
    public string SecondaryText => this.HasStarted ? Resources.Exit : this["SetupSkip"];
    public bool IsProgressVisible => this.IsLoading || this.IsInstalling;
    public bool IsCompleted { get; private set; }
    public event EventHandler? Completed;

    public PluginSetupViewModel(
        NuGetPluginService service, IConfiguration configuration, ILogger<PluginSetupViewModel> logger,
        Dispatcher? dispatcher = null)
    {
        this.service = service;
        this.configuration = configuration;
        this.logger = logger;
        this.dispatcher = dispatcher;
        this.service.PackageInformationUpdated += OnPackageInformationUpdated;
        ApplySnapshot();
    }

    private void OnPackageInformationUpdated(object? sender, EventArgs e)
    {
        if (this.dispatcher is { } dispatcher && !dispatcher.CheckAccess())
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
        foreach (var previous in previousSelections.Values)
        {
            previous.PropertyChanged -= OnPackagePropertyChanged;
            CancelReadmeLoad(previous);
        }
        var packages = snapshot.Packages.Where(package => package.IsOfficial
                && !BundledPackageIds.Contains(package.Id))
            .Select(info => new PluginSetupPackage(info, this.configuration))
            .ToArray();
        foreach (var package in packages)
        {
            if (previousSelections.TryGetValue(package.Package.Id, out var previous))
            {
                package.IsSelected = previous.IsSelected;
                package.IsExpanded = previous.IsExpanded;
            }
            package.PropertyChanged += OnPackagePropertyChanged;
            if (package.IsExpanded)
            {
                StartReadmeLoad(package);
            }
        }
        this.Groups = [.. packages.GroupBy(package => package.CategoryKey)
            .OrderBy(group => group.Key switch
            {
                "TranslateModule" => 0,
                "OcrModule" => 1,
                "PluginCategoryFilter" => 2,
                _ => 3,
            })
            .ThenBy(group => Resources.ResourceManager.GetString(group.Key, Resources.Culture))
            .Select(group => new PluginSetupGroup(group.Key, [.. group.OrderBy(package => package.Package.Title)]))];
        this.HasSearchError = snapshot.Error is not null;
        this.ErrorMessage = this.HasSearchError ? Resources.NuGetSearchFailed
            : !this.IsLoading && packages.Length == 0 ? this["SetupNoPackages"] : null;
    }

    private void OnPackagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PluginSetupPackage package || e.PropertyName != nameof(PluginSetupPackage.IsExpanded))
        {
            return;
        }
        if (package.IsExpanded)
        {
            StartReadmeLoad(package);
        }
        else
        {
            CancelReadmeLoad(package);
        }
    }

    private void StartReadmeLoad(PluginSetupPackage package)
    {
        if (this.disposed || package.Package.HasReadme || package.Package.IsReadmeLoading)
        {
            return;
        }
        var version = package.Package.LatestVersion ?? package.Package.PrereleaseVersion;
        if (version is null)
        {
            return;
        }
        var cancellation = new CancellationTokenSource();
        this.readmeLoads.Add(package, cancellation);
        package.Package.IsReadmeLoading = true;
        _ = LoadReadmeAsync(package, version, cancellation);
    }

    private async Task LoadReadmeAsync(PluginSetupPackage package, string version, CancellationTokenSource cancellation)
    {
        try
        {
            var readme = await this.service.GetPackageReadmeAsync(
                package.Package.Id, version, CultureInfo.CurrentUICulture, cancellation.Token);
            if (this.readmeLoads.TryGetValue(package, out var current) && ReferenceEquals(current, cancellation))
            {
                package.Package.ReadmeMarkdown = readme;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // 折りたたみ・一覧更新時のキャンセルは正常。
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "プラグインREADMEの取得に失敗しました: {PackageId} {Version}",
                package.Package.Id, version);
        }
        finally
        {
            if (this.readmeLoads.TryGetValue(package, out var current) && ReferenceEquals(current, cancellation))
            {
                this.readmeLoads.Remove(package);
                package.Package.IsReadmeLoading = false;
            }
            cancellation.Dispose();
        }
    }

    private void CancelReadmeLoad(PluginSetupPackage package)
    {
        if (this.readmeLoads.Remove(package, out var cancellation))
        {
            cancellation.Cancel();
            package.Package.IsReadmeLoading = false;
        }
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
        var packages = this.Groups.SelectMany(group => group.Packages)
            .Where(package => package.IsSelected && !package.Package.IsInstalled)
            .ToArray();
        var completedCount = 0;
        this.InstallProgress = 0;
        this.IsInstalling = packages.Length > 0;
        try
        {
            foreach (var package in packages)
            {
                package.ErrorMessage = null;
                try
                {
                    var version = package.Package.LatestVersion
                        ?? throw new InvalidOperationException(this["SetupSelectVersion"]);
                    var installed = await this.service.InstallSetupPackageAsync(
                        package.Package.Id, version,
                        new CallbackProgress<double>(value => SetInstallProgress(
                            (completedCount + Math.Clamp(value, 0, 100) / 100) / packages.Length * 100)));
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
                    completedCount++;
                    this.InstallProgress = (double)completedCount / packages.Length * 100;
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
            this.IsInstalling = false;
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

    private void SetInstallProgress(double value)
    {
        if (this.dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => this.InstallProgress = value);
        }
        else
        {
            this.InstallProgress = value;
        }
    }

    public void Dispose()
    {
        this.disposed = true;
        this.service.PackageInformationUpdated -= OnPackageInformationUpdated;
        foreach (var package in this.Groups.SelectMany(group => group.Packages))
        {
            package.PropertyChanged -= OnPackagePropertyChanged;
            CancelReadmeLoad(package);
        }
        this.Completed = null;
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}

internal sealed record PluginSetupGroup(string CategoryKey, IReadOnlyList<PluginSetupPackage> Packages)
{
    public string Name { get; } = CategoryKey switch
    {
        "TranslateModule" => Resources.PluginCategoryTranslate,
        "OcrModule" => "OCR",
        _ => Resources.ResourceManager.GetString(CategoryKey, Resources.Culture) ?? string.Empty,
    };
    public IReadOnlyList<PluginSetupHelpLink> HelpLinks { get; } = CategoryKey switch
    {
        "OcrModule" => [CreateHelpLink("OcrModule")],
        "TranslateModule" => [CreateHelpLink("TranslateModule")],
        _ => [],
    };

    private static PluginSetupHelpLink CreateHelpLink(string pageName)
        => new(
            HelpUriBuilder.Build(pageName, CultureInfo.CurrentUICulture),
            Resources.ResourceManager.GetString(pageName, Resources.Culture) ?? string.Empty);
}

internal sealed record PluginSetupHelpLink(string Uri, string ToolTip);

internal sealed partial class PluginSetupPackage : ObservableObject
{
    public PluginPackageViewModel Package { get; }
    public string CategoryKey { get; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(this.ErrorMessage);

    public PluginSetupPackage(NuGetPackageInfo info, IConfiguration configuration)
    {
        this.Package = new(info, false, null);
        this.CategoryKey = info.Tags.Contains("translate", StringComparer.OrdinalIgnoreCase) ? "TranslateModule"
            : info.Tags.Contains("ocr", StringComparer.OrdinalIgnoreCase) ? "OcrModule"
            : info.Tags.Contains("filter", StringComparer.OrdinalIgnoreCase) ? "PluginCategoryFilter"
            : "SetupOther";
        var modules = GetMigrationModules(info.Id);
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

    private static string[] GetMigrationModules(string packageId)
        => packageId.ToUpperInvariant() switch
        {
            "WINDOWTRANSLATOR.PLUGIN.TESSERACTOCRPLUGIN" => ["TesseractOcr"],
            "WINDOWTRANSLATOR.PLUGIN.DEEPLTRANSLATEPLUGIN" => ["DeepLTranslator"],
            "WINDOWTRANSLATOR.PLUGIN.GITHUBCOPILOTPLUGIN" => ["GitHubCopilotTranslator"],
            "WINDOWTRANSLATOR.PLUGIN.GOOGLEAPPSSCTIPTPLUGIN" => ["GasTranslator"],
            "WINDOWTRANSLATOR.PLUGIN.PLAMOPLUGIN" => ["PLaMoTranslator"],
            "WINDOWTRANSLATOR.PLUGIN.GOOGLEAIPLUGIN" => ["GoogleAITranslator", "GoogleAIOcr"],
            "WINDOWTRANSLATOR.PLUGIN.LLMPLUGIN" => ["LLMTranslator", "LLMOcr"],
            "WINDOWTRANSLATOR.PLUGIN.FOMPLUGIN" => ["FoMFilterModule"],
            _ => [],
        };
}
