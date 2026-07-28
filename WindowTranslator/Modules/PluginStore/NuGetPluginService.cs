using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGet V3 REST APIを使用してプラグインパッケージの検索・インストール・管理を行うサービスです。
/// </summary>
public sealed class NuGetPluginService : IDisposable
{
    private const string NuGetServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private const string PluginTag = "windowtranslator-plugin";
    private const string NuGetFlatContainerBase = "https://api.nuget.org/v3-flatcontainer";

    /// <summary>
    /// モジュール/パラメータクラス名からNuGetパッケージIDへのマッピング。
    /// アプリバンドルから除外されたプラグインの後方互換性自動インストールに使用します。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownClassToPackage =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // WindowTranslator.Plugin.FoMPlugin
            ["FoMFilterModule"] = "WindowTranslator.Plugin.FoMPlugin",
            ["FoMOptions"] = "WindowTranslator.Plugin.FoMPlugin",
            // WindowTranslator.Plugin.PLaMoPlugin
            ["PLaMoTranslator"] = "WindowTranslator.Plugin.PLaMoPlugin",
            ["PLaMoOptions"] = "WindowTranslator.Plugin.PLaMoPlugin",
            // WindowTranslator.Plugin.GitHubCopilotPlugin
            ["GitHubCopilotTranslator"] = "WindowTranslator.Plugin.GitHubCopilotPlugin",
            ["GitHubCopilotOptions"] = "WindowTranslator.Plugin.GitHubCopilotPlugin",
            // WindowTranslator.Plugin.DeepLTranslatePlugin
            ["DeepLTranslator"] = "WindowTranslator.Plugin.DeepLTranslatePlugin",
            ["DeepLOptions"] = "WindowTranslator.Plugin.DeepLTranslatePlugin",
            // WindowTranslator.Plugin.GoogleAIPlugin
            ["GoogleAITranslator"] = "WindowTranslator.Plugin.GoogleAIPlugin",
            ["GoogleAIOcr"] = "WindowTranslator.Plugin.GoogleAIPlugin",
            ["GoogleAIOptions"] = "WindowTranslator.Plugin.GoogleAIPlugin",
            // WindowTranslator.Plugin.GoogleAppsSctiptPlugin
            ["GasTranslator"] = "WindowTranslator.Plugin.GoogleAppsSctiptPlugin",
            ["GasOptions"] = "WindowTranslator.Plugin.GoogleAppsSctiptPlugin",
            // WindowTranslator.Plugin.LLMPlugin
            ["LLMTranslator"] = "WindowTranslator.Plugin.LLMPlugin",
            ["LLMOcr"] = "WindowTranslator.Plugin.LLMPlugin",
            ["LLMOptions"] = "WindowTranslator.Plugin.LLMPlugin",
            // WindowTranslator.Plugin.TesseractOCRPlugin
            ["TesseractOcr"] = "WindowTranslator.Plugin.TesseractOCRPlugin",
        };

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
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private string? searchUrl;

    public NuGetPluginService(ILogger<NuGetPluginService> logger)
        : this(
            logger,
            new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
            Path.Combine(PathUtility.UserDir, "plugins"),
            ownsHttpClient: true)
    {
    }

    internal NuGetPluginService(
        ILogger<NuGetPluginService> logger,
        HttpClient httpClient,
        string userPluginsDir,
        bool ownsHttpClient = false)
    {
        this.logger = logger;
        this.httpClient = httpClient;
        this.userPluginsDir = Path.GetFullPath(userPluginsDir);
        this.manifestPath = Path.Combine(this.userPluginsDir, "nuget-manifest.json");
        this.ownsHttpClient = ownsHttpClient;
    }

    /// <summary>
    /// 指定したパッケージの最新バージョンをインストールします。
    /// </summary>
    public async Task InstallLatestPackageAsync(string packageId, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var versionsUrl = $"{NuGetFlatContainerBase}/{packageId.ToLowerInvariant()}/index.json";
        using var response = await this.httpClient.GetAsync(versionsUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var versions = await JsonSerializer.DeserializeAsync<NuGetVersionListResponse>(content, JsonOptions, cancellationToken).ConfigureAwait(false);
        var parsedVersions = versions?.Versions?
            .Select(NuGetVersion.Parse)
            .OrderByDescending(v => v)
            .ToArray() ?? [];
        var latestVersion = parsedVersions.FirstOrDefault(v => !v.IsPrerelease)
            ?? parsedVersions.FirstOrDefault()
            ?? throw new InvalidOperationException($"パッケージ {packageId} のバージョン一覧を取得できませんでした。");
        await InstallPackageAsync(
            packageId,
            latestVersion.ToNormalizedString(),
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 設定ファイルで参照されているがインストールされていないプラグインを自動インストールします。
    /// アプリバンドルから除外されたプラグインの後方互換性維持のために使用します。
    /// </summary>
    public async Task AutoInstallFromSettingsAsync(string settingsPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsPath))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath, cancellationToken).ConfigureAwait(false));
            var neededPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (doc.RootElement.TryGetProperty("Targets", out var targets))
            {
                foreach (var target in targets.EnumerateObject())
                {
                    // SelectedPlugins の値（モジュールクラス名）をチェック
                    if (target.Value.TryGetProperty("SelectedPlugins", out var selectedPlugins))
                    {
                        foreach (var plugin in selectedPlugins.EnumerateObject())
                        {
                            var className = plugin.Value.GetString();
                            if (className is not null && KnownClassToPackage.TryGetValue(className, out var packageId))
                            {
                                neededPackages.Add(packageId);
                            }
                        }
                    }

                    // PluginParams のキー（パラメータクラス名）をチェック
                    if (target.Value.TryGetProperty("PluginParams", out var pluginParams))
                    {
                        foreach (var param in pluginParams.EnumerateObject())
                        {
                            if (KnownClassToPackage.TryGetValue(param.Name, out var packageId))
                            {
                                neededPackages.Add(packageId);
                            }
                        }
                    }
                }
            }

            if (neededPackages.Count == 0)
            {
                return;
            }

            var installed = await GetInstalledPackagesAsync(cancellationToken).ConfigureAwait(false);
            var installedIds = installed.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var packageId in neededPackages.Where(id => !installedIds.Contains(id)))
            {
                this.logger.LogInformation("設定で参照されているプラグインを自動インストール: {PackageId}", packageId);
                try
                {
                    await InstallLatestPackageAsync(packageId, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    this.logger.LogWarning(
                        ex,
                        "NuGetへ接続できないため、プラグインの自動インストールを中断します: {PackageId}",
                        packageId);
                    break;
                }
                catch (TaskCanceledException ex)
                {
                    this.logger.LogWarning(
                        ex,
                        "NuGet接続がタイムアウトしたため、プラグインの自動インストールを中断します: {PackageId}",
                        packageId);
                    break;
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "プラグイン {PackageId} の自動インストールに失敗しました。", packageId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "設定からのプラグイン自動インストール処理中にエラーが発生しました。");
        }
    }

    /// <summary>
    /// NuGetでWindowTranslatorプラグインを検索します。
    /// </summary>
    public async Task<IReadOnlyList<NuGetPackageInfo>> SearchPackagesAsync(CancellationToken cancellationToken = default)
    {
        if (this.searchUrl is null)
        {
            this.searchUrl = await GetSearchUrlAsync(cancellationToken).ConfigureAwait(false);
        }

        var url = $"{this.searchUrl}?q=tags:{PluginTag}&take=100&semVerLevel=2.0.0&prerelease=false";
        this.logger.LogDebug("NuGet検索URL: {Url}", url);

        using var response = await this.httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync<NuGetSearchResponse>(content, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("NuGet検索結果のデシリアライズに失敗しました。");

        this.logger.LogInformation("NuGet検索完了: {Count}件のパッケージが見つかりました。", result.Data?.Length ?? 0);

        return result.Data?.Select(d => new NuGetPackageInfo(
            Id: d.PackageId ?? string.Empty,
            Version: d.Version ?? string.Empty,
            Title: d.Title ?? d.PackageId ?? string.Empty,
            Description: d.Description ?? string.Empty,
            Authors: string.Join(", ", d.Authors ?? []),
            ProjectUrl: d.ProjectUrl,
            LicenseUrl: d.LicenseUrl
        )).ToArray() ?? [];
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
        var pendingDeleteMarker = GetPendingDeleteMarker(packageId);
        var markerWasPresent = false;
        string? markerContent = null;
        var targetMoved = false;
        var stagingMoved = false;
        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            markerWasPresent = File.Exists(pendingDeleteMarker);
            markerContent = markerWasPresent
                ? await File.ReadAllTextAsync(pendingDeleteMarker, cancellationToken).ConfigureAwait(false)
                : null;
            Directory.CreateDirectory(this.userPluginsDir);
            var installer = new NuGetPackageInstaller(this.httpClient, this.logger);
            await installer.InstallAsync(
                packageId,
                version,
                stagingDir,
                progress,
                cancellationToken).ConfigureAwait(false);

            var currentManifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
            var updatedManifest = AddOrUpdatePackage(currentManifest, packageId, version);

            if (markerWasPresent)
            {
                File.Delete(pendingDeleteMarker);
            }

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
                if (markerWasPresent && !File.Exists(pendingDeleteMarker))
                {
                    await File.WriteAllTextAsync(
                        pendingDeleteMarker,
                        markerContent ?? packageId,
                        CancellationToken.None).ConfigureAwait(false);
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
    /// 指定したパッケージをアンインストールします。（次回起動時に適用）
    /// </summary>
    public async Task UninstallPackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.logger.LogInformation("パッケージをアンインストール: {PackageId}", packageId);
            _ = GetPackageDirectory(packageId);
            Directory.CreateDirectory(this.userPluginsDir);

            var pendingDeleteMarker = GetPendingDeleteMarker(packageId);
            var markerAlreadyExisted = File.Exists(pendingDeleteMarker);
            var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
            var updatedManifest = RemovePackage(manifest, packageId);

            try
            {
                await WriteTextAtomicallyAsync(
                    pendingDeleteMarker,
                    packageId,
                    cancellationToken).ConfigureAwait(false);
                await SaveManifestAsync(updatedManifest, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (!markerAlreadyExisted)
                {
                    TryDeleteFile(pendingDeleteMarker);
                }
                throw;
            }

            this.logger.LogInformation(
                "パッケージ {PackageId} をアンインストールキューに追加しました。再起動後に完全に削除されます。",
                packageId);
        }
        finally
        {
            this.operationLock.Release();
        }
    }

    /// <summary>
    /// アプリ起動時にペンディング削除マーカーを処理します。
    /// </summary>
    public void ProcessPendingDeletions()
    {
        this.operationLock.Wait();
        try
        {
            if (!Directory.Exists(this.userPluginsDir))
            {
                return;
            }

            foreach (var markerFile in Directory.GetFiles(this.userPluginsDir, "*.pending-delete"))
            {
                try
                {
                    var packageId = File.ReadAllText(markerFile);
                    var markerPackageId = Path.GetFileName(markerFile)[..^".pending-delete".Length];
                    if (!packageId.Equals(markerPackageId, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("削除マーカーのパッケージIDがファイル名と一致しません。");
                    }

                    var targetDir = GetPackageDirectory(packageId);
                    if (Directory.Exists(targetDir))
                    {
                        Directory.Delete(targetDir, recursive: true);
                        this.logger.LogInformation("ペンディング削除を処理: {PackageId}", packageId);
                    }
                    File.Delete(markerFile);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "ペンディング削除の処理に失敗: {MarkerFile}", markerFile);
                }
            }
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

    private async Task<string> GetSearchUrlAsync(CancellationToken cancellationToken)
    {
        using var response = await this.httpClient.GetAsync(NuGetServiceIndexUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var index = await JsonSerializer.DeserializeAsync<NuGetServiceIndex>(content, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("NuGetサービスインデックスのデシリアライズに失敗しました。");

        var searchEntry = index.Resources?.FirstOrDefault(r => r.Type == "SearchQueryService/3.5.0")
            ?? index.Resources?.FirstOrDefault(r => r.Type?.StartsWith("SearchQueryService", StringComparison.Ordinal) == true)
            ?? throw new InvalidOperationException("NuGet検索サービスURLが見つかりませんでした。");

        return searchEntry.Id ?? throw new InvalidOperationException("NuGet検索サービスURLが空です。");
    }

    private static InstalledManifest AddOrUpdatePackage(
        InstalledManifest manifest,
        string packageId,
        string version)
    {
        var packages = manifest.Packages.ToList();
        var existing = packages.FindIndex(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        var newEntry = new InstalledPackageInfo(packageId, version, DateTime.UtcNow);
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
            return await JsonSerializer.DeserializeAsync<InstalledManifest>(
                fs,
                JsonOptions,
                cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("プラグインマニフェストが空です。");
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

    private static async Task WriteTextAtomicallyAsync(
        string destinationPath,
        string content,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{destinationPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
            ReplaceFile(temporaryPath, destinationPath);
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

    private string GetPendingDeleteMarker(string packageId)
    {
        _ = GetPackageDirectory(packageId);
        return Path.Combine(this.userPluginsDir, $"{packageId}.pending-delete");
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
    string? LicenseUrl
);

/// <summary>インストール済みパッケージ情報</summary>
public record InstalledPackageInfo(
    string Id,
    string Version,
    DateTime InstalledAt
);

/// <summary>インストール済みパッケージのマニフェスト</summary>
public record InstalledManifest(List<InstalledPackageInfo> Packages);

// NuGet V3 API レスポンス型
internal record NuGetServiceIndex(
    [property: JsonPropertyName("resources")] NuGetServiceResource[]? Resources
);

internal record NuGetServiceResource(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("@type")] string? Type
);

internal record NuGetSearchResponse(
    [property: JsonPropertyName("totalHits")] int TotalHits,
    [property: JsonPropertyName("data")] NuGetSearchData[]? Data
);

internal record NuGetSearchData(
    [property: JsonPropertyName("id")] string? PackageId,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("authors")] string[]? Authors,
    [property: JsonPropertyName("projectUrl")] string? ProjectUrl,
    [property: JsonPropertyName("licenseUrl")] string? LicenseUrl
);

internal record NuGetVersionListResponse(
    [property: JsonPropertyName("versions")] string[]? Versions
);
