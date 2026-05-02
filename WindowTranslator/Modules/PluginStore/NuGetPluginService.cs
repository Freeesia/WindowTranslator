using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGet V3 REST APIを使用してプラグインパッケージの検索・インストール・管理を行うサービスです。
/// </summary>
public sealed class NuGetPluginService : IDisposable
{
    private const string NuGetServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private const string PluginTag = "windowtranslator-plugin";
    private const string NuGetFlatContainerBase = "https://api.nuget.org/v3-flatcontainer";

    private static readonly string UserPluginsDir = Path.Combine(PathUtility.UserDir, "plugins");
    private static readonly string ManifestPath = Path.Combine(UserPluginsDir, "nuget-manifest.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient httpClient;
    private readonly ILogger<NuGetPluginService> logger;
    private string? searchUrl;

    public NuGetPluginService(ILogger<NuGetPluginService> logger)
    {
        this.httpClient = new HttpClient();
        this.logger = logger;
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

        var response = await this.httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
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
        var packageIdLower = packageId.ToLowerInvariant();
        var versionLower = version.ToLowerInvariant();
        var nupkgUrl = $"{NuGetFlatContainerBase}/{packageIdLower}/{versionLower}/{packageIdLower}.{versionLower}.nupkg";

        this.logger.LogInformation("パッケージをダウンロード中: {PackageId} {Version}", packageId, version);

        // 一時ディレクトリにダウンロード
        var tempDir = Path.Combine(Path.GetTempPath(), "WindowTranslatorPlugins", packageId);
        Directory.CreateDirectory(tempDir);
        var tempNupkgPath = Path.Combine(tempDir, $"{packageIdLower}.{versionLower}.nupkg");

        try
        {
            await DownloadFileAsync(nupkgUrl, tempNupkgPath, progress, cancellationToken).ConfigureAwait(false);

            // ターゲットディレクトリを準備
            var targetDir = Path.Combine(UserPluginsDir, packageId);
            // 古いファイルをバックアップして削除する前に一時フォルダへ移動
            var backupDir = $"{targetDir}.backup";
            if (Directory.Exists(targetDir))
            {
                if (Directory.Exists(backupDir))
                    Directory.Delete(backupDir, recursive: true);
                Directory.Move(targetDir, backupDir);
            }

            Directory.CreateDirectory(targetDir);

            try
            {
                // nupkgを展開して必要なDLLをコピー
                ExtractPluginDlls(tempNupkgPath, targetDir);
                this.logger.LogInformation("パッケージの展開完了: {PackageId} -> {TargetDir}", packageId, targetDir);
            }
            catch
            {
                // 失敗したら元に戻す
                Directory.Delete(targetDir, recursive: true);
                if (Directory.Exists(backupDir))
                    Directory.Move(backupDir, targetDir);
                throw;
            }

            // バックアップを削除
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, recursive: true);

            // マニフェストを更新
            await UpdateManifestAsync(packageId, version, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // 一時ファイルを削除
            try { File.Delete(tempNupkgPath); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// 指定したパッケージをアンインストールします。（次回起動時に適用）
    /// </summary>
    public async Task UninstallPackageAsync(string packageId, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation("パッケージをアンインストール: {PackageId}", packageId);

        var targetDir = Path.Combine(UserPluginsDir, packageId);
        // 実行中のDLLがロックされている可能性があるため、削除マーカーを置く
        var pendingDeleteMarker = Path.Combine(UserPluginsDir, $"{packageId}.pending-delete");
        await File.WriteAllTextAsync(pendingDeleteMarker, packageId, cancellationToken).ConfigureAwait(false);

        // マニフェストから削除
        await RemoveFromManifestAsync(packageId, cancellationToken).ConfigureAwait(false);

        this.logger.LogInformation("パッケージ {PackageId} をアンインストールキューに追加しました。再起動後に完全に削除されます。", packageId);
    }

    /// <summary>
    /// アプリ起動時にペンディング削除マーカーを処理します。
    /// </summary>
    public void ProcessPendingDeletions()
    {
        if (!Directory.Exists(UserPluginsDir))
            return;

        foreach (var markerFile in Directory.GetFiles(UserPluginsDir, "*.pending-delete"))
        {
            try
            {
                var packageId = File.ReadAllText(markerFile);
                var targetDir = Path.Combine(UserPluginsDir, packageId);
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
        var response = await this.httpClient.GetAsync(NuGetServiceIndexUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var index = await JsonSerializer.DeserializeAsync<NuGetServiceIndex>(content, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("NuGetサービスインデックスのデシリアライズに失敗しました。");

        var searchEntry = index.Resources?.FirstOrDefault(r => r.Type == "SearchQueryService/3.5.0")
            ?? index.Resources?.FirstOrDefault(r => r.Type?.StartsWith("SearchQueryService", StringComparison.Ordinal) == true)
            ?? throw new InvalidOperationException("NuGet検索サービスURLが見つかりませんでした。");

        return searchEntry.Id ?? throw new InvalidOperationException("NuGet検索サービスURLが空です。");
    }

    private async Task DownloadFileAsync(string url, string destPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var response = await this.httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var fileStream = File.Create(destPath);
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            downloadedBytes += bytesRead;
            if (totalBytes > 0)
            {
                progress?.Report((double)downloadedBytes / totalBytes);
            }
        }
    }

    private static void ExtractPluginDlls(string nupkgPath, string targetDir)
    {
        using var archive = ZipFile.OpenRead(nupkgPath);

        // 最適なTFMのlib/エントリを探す
        var libEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrEmpty(e.Name)
                     && e.Name != "_._")
            .ToList();

        if (!libEntries.Any())
        {
            throw new InvalidOperationException("パッケージにlib/フォルダが見つかりませんでした。");
        }

        // TFMを選択（net10.0-windows > net10.0 > net9.0-windows > net9.0 > ... の優先順位）
        var tfmGroups = libEntries
            .GroupBy(e => e.FullName.Split('/')[1])
            .ToList();

        var selectedTfm = SelectBestTfm([.. tfmGroups.Select(g => g.Key)]);
        if (selectedTfm is null)
        {
            throw new InvalidOperationException("互換性のあるターゲットフレームワークが見つかりませんでした。");
        }

        var selectedEntries = tfmGroups.First(g => g.Key == selectedTfm);

        foreach (var entry in selectedEntries)
        {
            var destPath = Path.Combine(targetDir, entry.Name);
            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    private static string? SelectBestTfm(string[] tfms)
    {
        // TFMの優先度リスト（.NET 10から降順、Windows版を優先）
        var orderedPrefixes = new[]
        {
            "net10.0-windows",
            "net10.0",
            "net9.0-windows",
            "net9.0",
            "net8.0-windows",
            "net8.0",
            "net7.0-windows",
            "net7.0",
            "net6.0-windows",
            "net6.0",
            "netstandard2.1",
            "netstandard2.0",
        };

        foreach (var prefix in orderedPrefixes)
        {
            // 完全一致または前方一致（例: net10.0-windows10.0.20348.0）
            var match = tfms.OrderByDescending(t => t).FirstOrDefault(t =>
                t.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || t.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return tfms.FirstOrDefault();
    }

    private async Task UpdateManifestAsync(string packageId, string version, CancellationToken cancellationToken)
    {
        var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        var packages = manifest.Packages.ToList();
        var existing = packages.FindIndex(p => p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
        var newEntry = new InstalledPackageInfo(packageId, version, DateTime.UtcNow);
        if (existing >= 0)
            packages[existing] = newEntry;
        else
            packages.Add(newEntry);

        await SaveManifestAsync(new InstalledManifest([.. packages]), cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveFromManifestAsync(string packageId, CancellationToken cancellationToken)
    {
        var manifest = await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        var packages = manifest.Packages.Where(p => !p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)).ToList();
        await SaveManifestAsync(new InstalledManifest([.. packages]), cancellationToken).ConfigureAwait(false);
    }

    private async Task<InstalledManifest> LoadManifestAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(ManifestPath))
            {
                using var fs = File.OpenRead(ManifestPath);
                var manifest = await JsonSerializer.DeserializeAsync<InstalledManifest>(fs, JsonOptions, cancellationToken).ConfigureAwait(false);
                return manifest ?? new InstalledManifest([]);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "プラグインマニフェストの読み込みに失敗しました。");
        }
        return new InstalledManifest([]);
    }

    private static async Task SaveManifestAsync(InstalledManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(UserPluginsDir);
        using var fs = File.Create(ManifestPath);
        await JsonSerializer.SerializeAsync(fs, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
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
