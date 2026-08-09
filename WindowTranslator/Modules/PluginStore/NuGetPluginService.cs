using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGetクライアントSDKを使用してプラグインパッケージの検索・インストール・管理を行うサービスです。
/// </summary>
public sealed class NuGetPluginService : BackgroundService
{
    internal const string NuGetServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    internal const string HttpClientName = "NuGetPluginReadme";
    internal const string PluginTag = "windowtranslator-plugin";
    internal const string AbstractionsPackageId = "WindowTranslator.Abstractions";
    private const int SearchResultLimit = 100;
    private const int MaxConcurrentMetadataRequests = 8;
    private static readonly TimeSpan PackageInformationRefreshInterval = TimeSpan.FromHours(1);

    internal static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly SourceRepository repository;
    private readonly ILogger<NuGetPluginService> logger;
    private readonly string nugetPluginsDir;
    private readonly string manifestPath;
    private readonly IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions;
    private readonly int hostMajorVersion;
    private readonly AsyncSemaphore operationLock = new(1);
    private readonly AsyncSemaphore refreshLock = new(1);
    private readonly object snapshotLock = new();
    private PluginStoreSnapshot packageSnapshot = PluginStoreSnapshot.Empty;

    internal NuGetPluginService(
        ILogger<NuGetPluginService> logger,
        IHttpClientFactory httpClientFactory,
        SourceRepository repository,
        string nugetPluginsDir,
        IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions,
        int hostMajorVersion)
    {
        this.logger = logger;
        this.httpClientFactory = httpClientFactory;
        this.repository = repository;
        this.nugetPluginsDir = Path.GetFullPath(nugetPluginsDir);
        this.manifestPath = Path.Combine(this.nugetPluginsDir, "nuget-manifest.json");
        this.hostPackageVersions = hostPackageVersions;
        this.hostMajorVersion = hostMajorVersion;
    }

    internal event EventHandler? PackageInformationUpdated;

    internal PluginStoreSnapshot PackageSnapshot
    {
        get
        {
            lock (this.snapshotLock)
            {
                return this.packageSnapshot;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshPackageInformationAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(PackageInformationRefreshInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    internal async Task RefreshPackageInformationAsync(CancellationToken cancellationToken = default)
    {
        using var refresh = await this.refreshLock.EnterAsync(cancellationToken);
        var previousSnapshot = this.PackageSnapshot;
        var packages = previousSnapshot.Packages;
        Exception? error = null;
        try
        {
            var searchResult = await SearchPackagesCoreAsync(cancellationToken).ConfigureAwait(false);
            packages = searchResult.Packages;
            error = searchResult.Error;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogWarning(ex, "NuGetからプラグイン情報を更新できませんでした。");
            error = ex;
        }

        using var operation = await this.operationLock.EnterAsync(cancellationToken);
        IReadOnlyList<InstalledPackageInfo> installedPackages;
        try
        {
            var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
            installedPackages = GetCompatibilityAwarePackages(manifest.Packages);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogWarning(ex, "インストール済みプラグイン情報を更新できませんでした。");
            installedPackages = this.PackageSnapshot.InstalledPackages;
            error = ex;
        }

        SetPackageSnapshot(new(
            InstalledPackages: installedPackages,
            Packages: packages,
            Error: error));
    }

    private async Task<(IReadOnlyList<NuGetPackageInfo> Packages, Exception? Error)> SearchPackagesCoreAsync(
        CancellationToken cancellationToken)
    {
        var searchResource = await this.repository
            .GetResourceAsync<PackageSearchResource>(cancellationToken)
            .ConfigureAwait(false);
        var metadataResource = await this.repository
            .GetResourceAsync<PackageMetadataResource>(cancellationToken)
            .ConfigureAwait(false);
        var searchResults = (await searchResource.SearchAsync(
                $"tags:{PluginTag}",
                new(includePrerelease: true) { IncludeDelisted = false },
                skip: 0,
                take: SearchResultLimit,
                NuGet.Common.NullLogger.Instance,
                cancellationToken).ConfigureAwait(false))
            .Where(metadata => !string.IsNullOrWhiteSpace(metadata.Identity?.Id))
            .ToArray();
        this.logger.LogInformation("NuGetタグ検索完了: {Count}件の候補が見つかりました。", searchResults.Length);

        var requestGate = new AsyncSemaphore(MaxConcurrentMetadataRequests);
        var packageTasks = searchResults.Select(async data =>
        {
            using var request = await requestGate.EnterAsync(cancellationToken);
            try
            {
                var package = await CreateCompatiblePackageInfoAsync(
                    data,
                    metadataResource,
                    cancellationToken).ConfigureAwait(false);
                return (Package: package, Error: (Exception?)null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var packageError = new InvalidOperationException(
                    $"NuGetパッケージ {data.Identity.Id} のメタデータを取得できませんでした。",
                    ex);
                this.logger.LogWarning(
                    packageError,
                    "NuGetパッケージのプラグイン互換性を確認できなかったため除外します: {PackageId}",
                    data.Identity.Id);
                return (Package: (NuGetPackageInfo?)null, Error: packageError);
            }
        });
        var results = await Task.WhenAll(packageTasks).ConfigureAwait(false);
        var compatiblePackages = results
            .Where(result => result.Package is not null)
            .Select(result => result.Package!)
            .ToArray();
        var errors = results
            .Where(result => result.Error is not null)
            .Select(result => result.Error!)
            .ToArray();
        var error = errors.Length == 0
            ? null
            : new AggregateException(
                "一部のNuGetパッケージ情報を取得できませんでした。",
                errors);

        this.logger.LogInformation(
            "NuGet互換性確認完了: {Count}件のWindowTranslatorプラグインが見つかりました。",
            compatiblePackages.Length);
        return new(compatiblePackages, error);
    }

    /// <summary>
    /// 指定したパッケージバージョンのREADMEを取得します。
    /// </summary>
    public async Task<string?> GetPackageReadmeAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentException("NuGetパッケージIDが空です。", nameof(packageId));
        }
        if (!NuGetVersion.TryParse(version, out var packageVersion))
        {
            throw new ArgumentException($"不正なNuGetパッケージバージョンです: {version}", nameof(version));
        }

        var metadataResource = await this.repository
            .GetResourceAsync<PackageMetadataResource>(cancellationToken)
            .ConfigureAwait(false);
        using var cacheContext = new SourceCacheContext();
        var metadata = await metadataResource.GetMetadataAsync(
            new PackageIdentity(packageId, packageVersion),
            cacheContext,
            NuGet.Common.NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);
        var readmeUrl = metadata?.ReadmeFileUrl;
        if (string.IsNullOrWhiteSpace(readmeUrl))
        {
            return null;
        }

        using var httpClient = this.httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.GetAsync(readmeUrl, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 指定したNuGetパッケージをインストールします。
    /// </summary>
    public async Task InstallPackageAsync(string packageId, string version, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        using var operation = await this.operationLock.EnterAsync(cancellationToken);
        var manifestExisted = File.Exists(this.manifestPath);
        var currentManifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        await using var pluginOperation = await NuGetPluginOperation.BeginAsync(
            this.nugetPluginsDir,
            packageId,
            manifestExisted ? currentManifest : null,
            cancellationToken).ConfigureAwait(false);

        var packageResource = await this.repository
            .GetResourceAsync<FindPackageByIdResource>(cancellationToken)
            .ConfigureAwait(false);
        var installer = new NuGetPackageInstaller(
            packageResource,
            this.logger,
            this.hostPackageVersions);
        await installer.InstallAsync(
            packageId,
            version,
            pluginOperation.WorkingPath,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (Directory.Exists(pluginOperation.TargetPath))
        {
            Directory.Move(pluginOperation.TargetPath, pluginOperation.BackupPath);
        }
        Directory.Move(pluginOperation.WorkingPath, pluginOperation.TargetPath);

        var updatedManifest = new InstalledManifest(
        [
            .. currentManifest.Packages.Where(package =>
                !package.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)),
            new(packageId, version, this.hostMajorVersion),
        ]);
        await SaveManifestAsync(updatedManifest, cancellationToken).ConfigureAwait(false);
        pluginOperation.Commit();
        UpdateInstalledPackages(updatedManifest.Packages);

        this.logger.LogInformation(
            "パッケージのインストール完了: {PackageId} {Version} -> {TargetDir}",
            packageId,
            version,
            pluginOperation.TargetPath);
    }

    /// <summary>
    /// 指定したパッケージを管理フォルダから削除します。
    /// 実行中のプラグインは一時フォルダから読み込まれているため、反映には再起動が必要です。
    /// </summary>
    public async Task UninstallPackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        using var operation = await this.operationLock.EnterAsync(cancellationToken);
        this.logger.LogInformation("パッケージをアンインストール: {PackageId}", packageId);
        var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        var updatedManifest = new InstalledManifest([.. manifest.Packages.Where(package =>
            !package.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase))]);
        await SaveManifestAsync(updatedManifest, cancellationToken).ConfigureAwait(false);
        UpdateInstalledPackages(updatedManifest.Packages);

        var targetPath = NuGetPluginOperation.GetPackageDirectory(this.nugetPluginsDir, packageId);
        if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, recursive: true);
        }

        this.logger.LogInformation(
            "パッケージ {PackageId} を管理フォルダからアンインストールしました。再起動後に反映されます。",
            packageId);
    }

    private async Task<NuGetPackageInfo?> CreateCompatiblePackageInfoAsync(
        IPackageSearchMetadata data,
        PackageMetadataResource metadataResource,
        CancellationToken cancellationToken)
    {
        var packageId = data.Identity.Id;
        using var cacheContext = new SourceCacheContext();
        var versions = await metadataResource.GetMetadataAsync(
            packageId,
            includePrerelease: true,
            includeUnlisted: false,
            cacheContext,
            NuGet.Common.NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);
        var compatibleVersions = versions
            .Where(version => version.Identity?.Version is not null
                && version.IsListed
                && HasCompatibleAbstractionsDependency(version.DependencySets))
            .OrderBy(version => version.Identity.Version)
            .ToArray();
        if (compatibleVersions.Length == 0)
        {
            this.logger.LogDebug(
                "WindowTranslator.Abstractionsへの互換依存がないため除外します: {PackageId}",
                packageId);
            return null;
        }

        return new NuGetPackageInfo(
            Id: packageId,
            Title: data.Title ?? packageId,
            Description: data.Description ?? string.Empty,
            Authors: data.Authors ?? string.Empty,
            ProjectUrl: data.ProjectUrl?.AbsoluteUri,
            LicenseUrl: data.LicenseUrl?.AbsoluteUri,
            Versions: compatibleVersions
                .Select(version => version.Identity.Version.ToNormalizedString())
                .ToArray(),
            IconUrl: data.IconUrl?.AbsoluteUri);
    }

    private bool HasCompatibleAbstractionsDependency(
        IEnumerable<PackageDependencyGroup>? dependencyGroups)
    {
        var dependencyGroup = NuGetPackageInstaller.SelectBestDependencyGroup(dependencyGroups ?? []);
        var dependency = dependencyGroup?.Packages.FirstOrDefault(item =>
            item.Id.Equals(AbstractionsPackageId, StringComparison.OrdinalIgnoreCase));
        if (dependency is null)
        {
            return false;
        }

        this.hostPackageVersions.TryGetValue(AbstractionsPackageId, out var hostVersion);
        return PluginCompatibility.IsVersionCompatible(dependency.VersionRange, hostVersion);
    }

    internal static IReadOnlyDictionary<string, NuGetVersion> CreateHostPackageVersions()
    {
        var abstractionsAssembly = typeof(UserSettings).Assembly;
        var informationalVersion = abstractionsAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion)
            && NuGetVersion.TryParse(informationalVersion, out var packageVersion))
        {
            return new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
            {
                [AbstractionsPackageId] = packageVersion,
            };
        }

        var assemblyVersion = abstractionsAssembly.GetName().Version
            ?? throw new InvalidOperationException(
                "WindowTranslator.Abstractions のバージョンを取得できませんでした。");
        var fallbackVersion = new NuGetVersion(
            assemblyVersion.Major,
            assemblyVersion.Minor,
            Math.Max(assemblyVersion.Build, 0));
        return new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
        {
            [AbstractionsPackageId] = fallbackVersion,
        };
    }

    private InstalledPackageInfo[] GetCompatibilityAwarePackages(
        IEnumerable<InstalledPackageInfo> packages)
        => packages
            .Select(package => package with
            {
                IsCompatible = PluginCompatibility.IsHostMajorCompatible(
                    package.HostMajorVersion,
                    this.hostMajorVersion),
            })
            .ToArray();

    private void UpdateInstalledPackages(IEnumerable<InstalledPackageInfo> packages)
    {
        lock (this.snapshotLock)
        {
            this.packageSnapshot = this.packageSnapshot with
            {
                InstalledPackages = GetCompatibilityAwarePackages(packages),
            };
        }
        NotifyPackageInformationUpdated();
    }

    private void SetPackageSnapshot(PluginStoreSnapshot snapshot)
    {
        lock (this.snapshotLock)
        {
            this.packageSnapshot = snapshot;
        }

        NotifyPackageInformationUpdated();
    }

    private void NotifyPackageInformationUpdated()
    {
        foreach (EventHandler handler in this.PackageInformationUpdated?.GetInvocationList()
                     .Cast<EventHandler>() ?? [])
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "プラグイン情報更新イベントの通知に失敗しました。");
            }
        }
    }

    private async Task<InstalledManifest> LoadManifestAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(this.manifestPath))
        {
            return new InstalledManifest([]);
        }

        try
        {
            await using var fs = File.OpenRead(this.manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<InstalledManifest>(
                fs,
                ManifestJsonOptions,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("プラグインマニフェストが空です。");
            if (manifest.Packages is null)
            {
                throw new InvalidDataException(
                    "プラグインマニフェストにインストール済みパッケージ一覧がありません。");
            }

            return manifest;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogWarning(ex, "プラグインマニフェストの読み込みに失敗しました。");
            throw new InvalidOperationException("プラグインマニフェストを読み込めませんでした。", ex);
        }
    }

    private Task SaveManifestAsync(InstalledManifest manifest, CancellationToken cancellationToken)
        => NuGetPluginOperation.SaveManifestAsync(
            this.manifestPath,
            manifest,
            cancellationToken);

}

/// <summary>NuGetパッケージ情報</summary>
public record NuGetPackageInfo(
    string Id,
    string Title,
    string Description,
    string Authors,
    string? ProjectUrl,
    string? LicenseUrl,
    IReadOnlyList<string> Versions,
    string? IconUrl = null
);

/// <summary>インストール済みパッケージ情報</summary>
public record InstalledPackageInfo(
    string Id,
    string Version,
    [property: JsonRequired] int HostMajorVersion)
{
    [JsonIgnore]
    public bool IsCompatible { get; init; } = true;
}

/// <summary>NuGetプラグインの管理マニフェスト</summary>
public record InstalledManifest(List<InstalledPackageInfo> Packages);

internal sealed record PluginStoreSnapshot(
    IReadOnlyList<InstalledPackageInfo> InstalledPackages,
    IReadOnlyList<NuGetPackageInfo> Packages,
    Exception? Error)
{
    public static PluginStoreSnapshot Empty { get; } = new([], [], null);
}
