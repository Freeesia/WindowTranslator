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
    internal const string OperationsDirectoryName = ".operations";
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
    private readonly string operationsDir;
    private readonly string manifestPath;
    private readonly IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions;
    private readonly int hostMajorVersion;
    private readonly AsyncSemaphore operationLock = new(1);
    private readonly AsyncSemaphore refreshLock = new(1);
    private readonly object snapshotLock = new();
    private PluginStoreSnapshot packageSnapshot = PluginStoreSnapshot.Empty;
    private long installedPackagesGeneration;

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
        this.operationsDir = Path.Combine(this.nugetPluginsDir, OperationsDirectoryName);
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

        var installedGenerationBefore = Volatile.Read(ref this.installedPackagesGeneration);
        IReadOnlyList<InstalledPackageInfo> installedPackages;
        try
        {
            installedPackages = await GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.logger.LogWarning(ex, "インストール済みプラグイン情報を更新できませんでした。");
            installedPackages = this.PackageSnapshot.InstalledPackages;
            error = ex;
        }

        var installedGenerationAfter = Volatile.Read(ref this.installedPackagesGeneration);
        if (installedGenerationBefore != installedGenerationAfter)
        {
            installedPackages = this.PackageSnapshot.InstalledPackages;
        }
        SetPackageSnapshot(
            new(
                IsInitialized: true,
                InstalledPackages: installedPackages,
                Packages: packages,
                Error: error),
            installedGenerationAfter);
    }

    /// <summary>
    /// NuGetでWindowTranslatorプラグインを検索します。
    /// </summary>
    public async Task<IReadOnlyList<NuGetPackageInfo>> SearchPackagesAsync(CancellationToken cancellationToken = default)
    {
        var result = await SearchPackagesCoreAsync(cancellationToken).ConfigureAwait(false);
        if (result.Error is not null)
        {
            throw result.Error;
        }

        return result.Packages;
    }

    private async Task<PackageSearchResult> SearchPackagesCoreAsync(
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
                return new PackageMetadataResult(package, null);
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
                return new PackageMetadataResult(null, packageError);
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
        var operationId = Guid.NewGuid().ToString("N");
        var targetDir = GetPackageDirectory(packageId);
        var stagingDir = Path.Combine(this.operationsDir, $"{packageId}.installing-{operationId}");
        var backupDir = Path.Combine(this.operationsDir, $"{packageId}.backup-{operationId}");
        var targetMoved = false;
        var stagingMoved = false;
        using var operation = await this.operationLock.EnterAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(this.operationsDir);
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
                stagingDir,
                progress,
                cancellationToken).ConfigureAwait(false);

            var currentManifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
            var updatedManifest = AddOrUpdatePackage(currentManifest, packageId, version);

            if (Directory.Exists(targetDir))
            {
                Directory.Move(targetDir, backupDir);
                targetMoved = true;
            }

            Directory.Move(stagingDir, targetDir);
            stagingMoved = true;
            await SaveManifestAsync(updatedManifest, cancellationToken).ConfigureAwait(false);
            UpdateInstalledPackages(updatedManifest.Packages);

            try
            {
                if (Directory.Exists(backupDir))
                {
                    Directory.Delete(backupDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "プラグインバックアップの削除に失敗しました: {BackupDir}", backupDir);
            }

            this.logger.LogInformation(
                "パッケージのインストール完了: {PackageId} {Version} -> {TargetDir}",
                packageId,
                version,
                targetDir);
        }
        catch
        {
            try
            {
                if (stagingMoved && Directory.Exists(targetDir))
                {
                    Directory.Move(targetDir, stagingDir);
                }
                if (targetMoved && Directory.Exists(backupDir))
                {
                    Directory.Move(backupDir, targetDir);
                }
            }
            catch (Exception rollbackException)
            {
                this.logger.LogError(
                    rollbackException,
                    "プラグイン {PackageId} のインストール失敗後の復旧に失敗しました。",
                    packageId);
            }
            throw;
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    /// <summary>
    /// 指定したパッケージを管理フォルダから削除します。
    /// 実行中のプラグインは一時フォルダから読み込まれているため、反映には再起動が必要です。
    /// </summary>
    public async Task UninstallPackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var targetDir = GetPackageDirectory(packageId);
        var uninstallingDir = Path.Combine(this.operationsDir, $"{packageId}.uninstalling-{operationId}");
        var targetMoved = false;
        using var operation = await this.operationLock.EnterAsync(cancellationToken);
        this.logger.LogInformation("パッケージをアンインストール: {PackageId}", packageId);
        Directory.CreateDirectory(this.operationsDir);

        var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        var updatedManifest = RemovePackage(manifest, packageId);

        if (Directory.Exists(targetDir))
        {
            Directory.Move(targetDir, uninstallingDir);
            targetMoved = true;
        }

        try
        {
            await SaveManifestAsync(updatedManifest, cancellationToken).ConfigureAwait(false);
            UpdateInstalledPackages(updatedManifest.Packages);
        }
        catch
        {
            try
            {
                if (targetMoved
                    && Directory.Exists(uninstallingDir)
                    && !Directory.Exists(targetDir))
                {
                    Directory.Move(uninstallingDir, targetDir);
                }
            }
            catch (Exception rollbackException)
            {
                this.logger.LogError(
                    rollbackException,
                    "プラグイン {PackageId} のアンインストール失敗後の復旧に失敗しました。",
                    packageId);
            }
            throw;
        }

        try
        {
            if (Directory.Exists(uninstallingDir))
            {
                Directory.Delete(uninstallingDir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(
                ex,
                "アンインストール済みプラグインフォルダの削除に失敗しました: {Directory}",
                uninstallingDir);
        }

        this.logger.LogInformation(
            "パッケージ {PackageId} を管理フォルダからアンインストールしました。再起動後に反映されます。",
            packageId);
    }

    /// <summary>
    /// インストール済みのパッケージ一覧を取得します。
    /// </summary>
    public async Task<IReadOnlyList<InstalledPackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        using var operation = await this.operationLock.EnterAsync(cancellationToken);
        var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        var migratedPackages = manifest.Packages
            .Select(package => package.HostMajorVersion is null
                ? package with { HostMajorVersion = this.hostMajorVersion }
                : package)
            .ToList();
        if (migratedPackages.Where((package, index) =>
                package != manifest.Packages[index]).Any())
        {
            manifest = new InstalledManifest(migratedPackages);
            await SaveManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
        }

        return GetCompatibilityAwarePackages(manifest.Packages);
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

        var latestVersion = compatibleVersions[^1].Identity.Version.ToNormalizedString();
        return new NuGetPackageInfo(
            Id: packageId,
            Version: latestVersion,
            Title: data.Title ?? packageId,
            Description: data.Description ?? string.Empty,
            Authors: data.Authors ?? string.Empty,
            ProjectUrl: data.ProjectUrl?.AbsoluteUri,
            LicenseUrl: data.LicenseUrl?.AbsoluteUri,
            Versions: compatibleVersions
                .Select(version => version.Identity.Version.ToNormalizedString())
                .ToArray());
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

    private InstalledManifest AddOrUpdatePackage(
        InstalledManifest manifest,
        string packageId,
        string version)
    {
        var packages = manifest.Packages.ToList();
        var existing = packages.FindIndex(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        var newEntry = new InstalledPackageInfo(
            packageId,
            version,
            this.hostMajorVersion);
        if (existing >= 0)
        {
            packages[existing] = newEntry;
        }
        else
        {
            packages.Add(newEntry);
        }

        return new InstalledManifest([.. packages]);
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
            this.installedPackagesGeneration++;
            this.packageSnapshot = this.packageSnapshot with
            {
                InstalledPackages = GetCompatibilityAwarePackages(packages),
            };
        }
        NotifyPackageInformationUpdated();
    }

    private void SetPackageSnapshot(
        PluginStoreSnapshot snapshot,
        long? expectedInstalledPackagesGeneration = null)
    {
        lock (this.snapshotLock)
        {
            if (expectedInstalledPackagesGeneration is not null
                && expectedInstalledPackagesGeneration != this.installedPackagesGeneration)
            {
                snapshot = snapshot with
                {
                    InstalledPackages = this.packageSnapshot.InstalledPackages,
                };
            }
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

    private static InstalledManifest RemovePackage(InstalledManifest manifest, string packageId)
        => new([.. manifest.Packages.Where(p =>
            !p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase))]);

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

    private async Task SaveManifestAsync(InstalledManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(this.nugetPluginsDir);
        var temporaryPath = $"{this.manifestPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var fs = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    fs,
                    manifest,
                    ManifestJsonOptions,
                    cancellationToken).ConfigureAwait(false);
                await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            ReplaceFile(temporaryPath, this.manifestPath);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static void ReplaceFile(string sourcePath, string destinationPath)
        => File.Move(sourcePath, destinationPath, overwrite: true);

    private string GetPackageDirectory(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)
            || packageId is "." or ".."
            || packageId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || packageId.Contains(Path.DirectorySeparatorChar)
            || packageId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"不正なNuGetパッケージIDです: {packageId}");
        }

        var root = this.nugetPluginsDir
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var packageDirectory = Path.GetFullPath(Path.Combine(root, packageId));
        if (!packageDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"不正なNuGetパッケージIDです: {packageId}");
        }

        return packageDirectory;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // 後始末の失敗は元の処理結果へ影響させない
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 後始末の失敗は元の処理結果へ影響させない
        }
    }

    private sealed record PackageMetadataResult(
        NuGetPackageInfo? Package,
        Exception? Error);

    private sealed record PackageSearchResult(
        IReadOnlyList<NuGetPackageInfo> Packages,
        Exception? Error);
}

/// <summary>NuGetパッケージ情報</summary>
public record NuGetPackageInfo(
    string Id,
    string Version,
    string Title,
    string Description,
    string Authors,
    string? ProjectUrl,
    string? LicenseUrl,
    IReadOnlyList<string>? Versions = null
);

/// <summary>インストール済みパッケージ情報</summary>
public record InstalledPackageInfo(
    string Id,
    string Version,
    int? HostMajorVersion = null)
{
    [JsonIgnore]
    public bool IsCompatible { get; init; } = true;
}

/// <summary>NuGetプラグインの管理マニフェスト</summary>
public record InstalledManifest(List<InstalledPackageInfo> Packages);

internal sealed record PluginStoreSnapshot(
    bool IsInitialized,
    IReadOnlyList<InstalledPackageInfo> InstalledPackages,
    IReadOnlyList<NuGetPackageInfo> Packages,
    Exception? Error)
{
    public static PluginStoreSnapshot Empty { get; } = new(false, [], [], null);
}
