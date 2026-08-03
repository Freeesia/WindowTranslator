using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NuGet.Packaging;
using NuGet.Versioning;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGet V3 REST APIを使用してプラグインパッケージの検索・インストール・管理を行うサービスです。
/// </summary>
public sealed class NuGetPluginService : IDisposable
{
    private const string NuGetServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    internal const string PluginTag = "windowtranslator-plugin";
    internal const string AbstractionsPackageId = "WindowTranslator.Abstractions";
    private const int MaxConcurrentMetadataRequests = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient httpClient;
    private readonly ILogger<NuGetPluginService> logger;
    private readonly string userPluginsDir;
    private readonly string manifestPath;
    private readonly bool ownsHttpClient;
    private readonly IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions;
    private readonly INuGetPluginMetadataSource metadataSource;
    private readonly SemaphoreSlim operationLock = new(1, 1);

    public NuGetPluginService(ILogger<NuGetPluginService> logger)
        : this(
            logger,
            new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
            })
            {
                Timeout = TimeSpan.FromSeconds(30),
            },
            Path.Combine(PathUtility.UserDir, "plugins"),
            ownsHttpClient: true)
    {
    }

    internal NuGetPluginService(
        ILogger<NuGetPluginService> logger,
        HttpClient httpClient,
        string userPluginsDir,
        bool ownsHttpClient = false,
        IReadOnlyDictionary<string, NuGetVersion>? hostPackageVersions = null,
        INuGetPluginMetadataSource? metadataSource = null)
    {
        this.logger = logger;
        this.httpClient = httpClient;
        this.userPluginsDir = Path.GetFullPath(userPluginsDir);
        this.manifestPath = Path.Combine(this.userPluginsDir, "nuget-manifest.json");
        this.ownsHttpClient = ownsHttpClient;
        this.hostPackageVersions = hostPackageVersions ?? CreateHostPackageVersions();
        this.metadataSource = metadataSource
            ?? new NuGetProtocolPluginMetadataSource(NuGetServiceIndexUrl);
    }

    /// <summary>
    /// NuGetでWindowTranslatorプラグインを検索します。
    /// </summary>
    public async Task<IReadOnlyList<NuGetPackageInfo>> SearchPackagesAsync(CancellationToken cancellationToken = default)
    {
        var searchResults = await this.metadataSource
            .SearchAsync(PluginTag, includePrerelease: true, cancellationToken)
            .ConfigureAwait(false);
        this.logger.LogInformation("NuGetタグ検索完了: {Count}件の候補が見つかりました。", searchResults.Count);

        using var requestGate = new SemaphoreSlim(MaxConcurrentMetadataRequests);
        var packageTasks = searchResults.Select(async data =>
        {
            await requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await CreateCompatiblePackageInfoAsync(
                    data,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(
                    ex,
                    "NuGetパッケージのプラグイン互換性を確認できなかったため除外します: {PackageId}",
                    data.Id);
                return null;
            }
            finally
            {
                requestGate.Release();
            }
        });
        var packages = await Task.WhenAll(packageTasks).ConfigureAwait(false);
        var compatiblePackages = packages.Where(package => package is not null).Select(package => package!).ToArray();

        this.logger.LogInformation(
            "NuGet互換性確認完了: {Count}件のWindowTranslatorプラグインが見つかりました。",
            compatiblePackages.Length);
        return compatiblePackages;
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

        var readmeUrl = await this.metadataSource
            .GetReadmeUrlAsync(packageId, packageVersion, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(readmeUrl))
        {
            return null;
        }
        using var response = await this.httpClient.GetAsync(readmeUrl, cancellationToken).ConfigureAwait(false);
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
        var stagingDir = Path.Combine(this.userPluginsDir, $".{packageId}.installing-{operationId}");
        var backupDir = $"{targetDir}.backup-{operationId}";
        var targetMoved = false;
        var stagingMoved = false;
        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(this.userPluginsDir);
            var installer = new NuGetPackageInstaller(
                this.httpClient,
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
            this.operationLock.Release();
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
        var uninstallingDir = $"{targetDir}.uninstalling-{operationId}";
        var targetMoved = false;
        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.logger.LogInformation("パッケージをアンインストール: {PackageId}", packageId);
            Directory.CreateDirectory(this.userPluginsDir);

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
        finally
        {
            this.operationLock.Release();
        }
    }

    /// <summary>
    /// インストール済みのパッケージ一覧を取得します。
    /// </summary>
    public async Task<IReadOnlyList<InstalledPackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        return manifest.Packages;
    }

    private async Task<NuGetPackageInfo?> CreateCompatiblePackageInfoAsync(
        NuGetPluginSearchMetadata data,
        CancellationToken cancellationToken)
    {
        var versions = await this.metadataSource
            .GetPackageVersionsAsync(data.Id, cancellationToken)
            .ConfigureAwait(false);
        var compatibleVersions = versions
            .Where(version => version.IsListed
                && HasCompatibleAbstractionsDependency(version.DependencyGroups))
            .OrderBy(version => version.Version)
            .ToArray();
        if (compatibleVersions.Length == 0)
        {
            this.logger.LogDebug(
                "WindowTranslator.Abstractionsへの互換依存がないため除外します: {PackageId}",
                data.Id);
            return null;
        }

        var latestVersion = compatibleVersions[^1].Version.ToNormalizedString();
        return new NuGetPackageInfo(
            Id: data.Id,
            Version: latestVersion,
            Title: data.Title ?? data.Id,
            Description: data.Description ?? string.Empty,
            Authors: data.Authors ?? string.Empty,
            ProjectUrl: data.ProjectUrl,
            LicenseUrl: data.LicenseUrl,
            Versions: compatibleVersions
                .Select(version => version.Version.ToNormalizedString())
                .ToArray());
    }

    private bool HasCompatibleAbstractionsDependency(
        IReadOnlyList<PackageDependencyGroup> dependencyGroups)
    {
        if (!this.hostPackageVersions.TryGetValue(AbstractionsPackageId, out var hostVersion))
        {
            return false;
        }

        var dependencyGroup = NuGetPackageInstaller.SelectBestDependencyGroup(dependencyGroups);
        var dependency = dependencyGroup?.Packages.FirstOrDefault(item =>
            item.Id.Equals(AbstractionsPackageId, StringComparison.OrdinalIgnoreCase));
        if (dependency is null)
        {
            return false;
        }

        return dependency.VersionRange?.Satisfies(hostVersion) is not false;
    }

    private static InstalledManifest AddOrUpdatePackage(
        InstalledManifest manifest,
        string packageId,
        string version)
    {
        var packages = manifest.Packages.ToList();
        var existing = packages.FindIndex(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        var newEntry = new InstalledPackageInfo(packageId, version);
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

    private static Dictionary<string, NuGetVersion> CreateHostPackageVersions()
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
                JsonOptions,
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
        Directory.CreateDirectory(this.userPluginsDir);
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
                await JsonSerializer.SerializeAsync(fs, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
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

        var root = this.userPluginsDir
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

    public void Dispose()
    {
        if (this.ownsHttpClient)
        {
            this.httpClient.Dispose();
        }
        this.operationLock.Dispose();
    }
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
    string Version
);

/// <summary>NuGetプラグインの管理マニフェスト</summary>
public record InstalledManifest(List<InstalledPackageInfo> Packages);
