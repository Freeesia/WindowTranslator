using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGetパッケージとそのランタイム依存関係を、プラグインフォルダへ展開します。
/// </summary>
internal sealed class NuGetPackageInstaller(
    HttpClient httpClient,
    ILogger logger,
    IReadOnlyDictionary<string, NuGetVersion>? hostPackageVersions = null)
{
    private const string FlatContainerBase = "https://api.nuget.org/v3-flatcontainer";

    private static readonly string[] CompatibleFrameworks =
    [
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
        "net5.0-windows",
        "net5.0",
        "netcoreapp3.1",
        "netstandard2.1",
        "netstandard2.0",
    ];

    private static readonly string[] CompatibleRuntimeIdentifiers = ["win-x64", "win", "any"];

    private readonly HttpClient httpClient = httpClient;
    private readonly ILogger logger = logger;
    private readonly IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions =
        hostPackageVersions ?? new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

    public async Task InstallAsync(
        string packageId,
        string version,
        string destinationDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ValidatePackageId(packageId);
        var requestedVersion = NuGetVersion.Parse(version);
        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "WindowTranslatorPlugins",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);

        try
        {
            var artifacts = await ResolvePackageGraphAsync(
                packageId,
                requestedVersion,
                workDirectory,
                progress,
                cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(destinationDirectory);
            foreach (var artifact in artifacts.OrderByDescending(a =>
                         a.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
            {
                ExtractPackageAssets(
                    artifact.PackagePath,
                    destinationDirectory,
                    requirePluginAssembly: artifact.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    internal static string? SelectBestTfm(IEnumerable<string> frameworks)
    {
        var candidates = frameworks
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(original => (Original: original, Normalized: NormalizeFramework(original)))
            .ToArray();

        foreach (var compatibleFramework in CompatibleFrameworks)
        {
            var match = candidates
                .OrderByDescending(c => c.Normalized, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(c => compatibleFramework.EndsWith("-windows", StringComparison.Ordinal)
                    ? c.Normalized.StartsWith(compatibleFramework, StringComparison.OrdinalIgnoreCase)
                    : c.Normalized.Equals(compatibleFramework, StringComparison.OrdinalIgnoreCase));
            if (match.Original is not null)
            {
                return match.Original;
            }
        }

        return null;
    }

    private async Task<IReadOnlyCollection<PackageArtifact>> ResolvePackageGraphAsync(
        string rootPackageId,
        NuGetVersion rootVersion,
        string workDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var constraints = new Dictionary<string, List<DependencyConstraint>>(StringComparer.OrdinalIgnoreCase);
        var selectedVersions = new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);
        var artifacts = new Dictionary<string, PackageArtifact>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        var queued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddConstraint(
            rootPackageId,
            "$root",
            new VersionRange(
                rootVersion,
                includeMinVersion: true,
                maxVersion: rootVersion,
                includeMaxVersion: true));

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentId = queue.Dequeue();
            queued.Remove(currentId);

            if (!constraints.TryGetValue(currentId, out var currentConstraints)
                || currentConstraints.Count == 0)
            {
                if (selectedVersions.Remove(currentId))
                {
                    artifacts.Remove(currentId);
                    RemoveConstraintsFrom(currentId);
                }
                continue;
            }

            var ranges = currentConstraints.Select(c => c.Range).ToArray();
            var resolvedVersion = currentId.Equals(rootPackageId, StringComparison.OrdinalIgnoreCase)
                ? rootVersion
                : await ResolveDependencyVersionAsync(currentId, ranges, cancellationToken).ConfigureAwait(false);

            if (!ranges.All(r => r.Satisfies(resolvedVersion)))
            {
                throw new InvalidOperationException(
                    $"パッケージ {currentId} の依存バージョンを解決できませんでした: {string.Join(", ", ranges)}");
            }

            if (selectedVersions.TryGetValue(currentId, out var existingVersion)
                && existingVersion == resolvedVersion)
            {
                continue;
            }

            if (selectedVersions.ContainsKey(currentId))
            {
                RemoveConstraintsFrom(currentId);
            }
            selectedVersions[currentId] = resolvedVersion;
            var packagePath = Path.Combine(
                workDirectory,
                $"{currentId.ToLowerInvariant()}.{resolvedVersion.ToNormalizedString().ToLowerInvariant()}.nupkg");
            await DownloadPackageAsync(
                currentId,
                resolvedVersion,
                packagePath,
                currentId.Equals(rootPackageId, StringComparison.OrdinalIgnoreCase) ? progress : null,
                cancellationToken).ConfigureAwait(false);
            ValidateHostPackageDependencies(
                packagePath,
                currentId,
                resolvedVersion,
                this.hostPackageVersions);

            artifacts[currentId] = new PackageArtifact(currentId, resolvedVersion, packagePath);
            foreach (var dependency in ReadRuntimeDependencies(packagePath))
            {
                if (this.hostPackageVersions.ContainsKey(dependency.Id))
                {
                    continue;
                }

                AddConstraint(dependency.Id, currentId, dependency.VersionRange);
            }
        }

        return artifacts.Values.ToArray();

        void AddConstraint(string id, string source, VersionRange range)
        {
            ValidatePackageId(id);
            if (!constraints.TryGetValue(id, out var packageConstraints))
            {
                packageConstraints = [];
                constraints[id] = packageConstraints;
            }

            if (packageConstraints.Any(c =>
                c.Source.Equals(source, StringComparison.OrdinalIgnoreCase)
                && c.Range.ToString().Equals(range.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            packageConstraints.Add(new DependencyConstraint(source, range));
            Enqueue(id);
        }

        void RemoveConstraintsFrom(string source)
        {
            foreach (var (id, packageConstraints) in constraints)
            {
                if (packageConstraints.RemoveAll(c =>
                    c.Source.Equals(source, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    Enqueue(id);
                }
            }
        }

        void Enqueue(string id)
        {
            if (queued.Add(id))
            {
                queue.Enqueue(id);
            }
        }
    }

    private async Task<NuGetVersion> ResolveDependencyVersionAsync(
        string packageId,
        IReadOnlyCollection<VersionRange> ranges,
        CancellationToken cancellationToken)
    {
        var versionsUrl = $"{FlatContainerBase}/{packageId.ToLowerInvariant()}/index.json";
        using var response = await this.httpClient.GetAsync(versionsUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var versionList = await JsonSerializer.DeserializeAsync<VersionIndex>(
            content,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var compatibleVersions = versionList?.Versions?
            .Select(NuGetVersion.Parse)
            .Where(v => ranges.All(r => r.Satisfies(v)))
            .OrderBy(v => v)
            .ToArray() ?? [];
        return compatibleVersions.FirstOrDefault(v => !v.IsPrerelease)
            ?? compatibleVersions.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"パッケージ {packageId} の依存条件を満たすバージョンがありません: {string.Join(", ", ranges)}");
    }

    private async Task DownloadPackageAsync(
        string packageId,
        NuGetVersion version,
        string destinationPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var packageIdLower = packageId.ToLowerInvariant();
        var versionLower = version.ToNormalizedString().ToLowerInvariant();
        var url = $"{FlatContainerBase}/{packageIdLower}/{versionLower}/{packageIdLower}.{versionLower}.nupkg";
        this.logger.LogInformation("NuGetパッケージをダウンロード中: {PackageId} {Version}", packageId, version);

        using var response = await this.httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            downloadedBytes += bytesRead;
            if (totalBytes > 0)
            {
                progress?.Report((double)downloadedBytes / totalBytes);
            }
        }
    }

    private static List<PackageDependency> ReadRuntimeDependencies(string packagePath)
        => ReadDependencies(packagePath, runtimeOnly: true);

    private static List<PackageDependency> ReadPackageDependencies(string packagePath)
        => ReadDependencies(packagePath, runtimeOnly: false);

    private static List<PackageDependency> ReadDependencies(
        string packagePath,
        bool runtimeOnly)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var nuspecEntry = archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("パッケージにnuspecが見つかりませんでした。");

        using var stream = nuspecEntry.Open();
        var document = XDocument.Load(stream);
        var metadata = document.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata")
            ?? throw new InvalidOperationException("nuspecのmetadataが見つかりませんでした。");
        var dependencies = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "dependencies");
        if (dependencies is null)
        {
            return [];
        }

        var result = new List<PackageDependency>();
        result.AddRange(ParseDependencyElements(
            dependencies.Elements().Where(e => e.Name.LocalName == "dependency"),
            runtimeOnly));

        var groups = dependencies.Elements()
            .Where(e => e.Name.LocalName == "group")
            .Select(e => (
                Element: e,
                Framework: e.Attribute("targetFramework")?.Value))
            .ToArray();
        if (groups.Length == 0)
        {
            return result;
        }

        var frameworkGroups = groups.Where(g =>
            !string.IsNullOrWhiteSpace(g.Framework)
            && !g.Framework.Equals("any", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (frameworkGroups.Length > 0)
        {
            var selectedFramework = SelectBestTfm(frameworkGroups.Select(g => g.Framework!));
            if (selectedFramework is not null)
            {
                result.AddRange(ParseDependencyElements(
                    frameworkGroups.First(g => string.Equals(
                        g.Framework,
                        selectedFramework,
                        StringComparison.OrdinalIgnoreCase)).Element.Elements(),
                    runtimeOnly));
                return result;
            }
        }

        var fallbackGroup = groups.FirstOrDefault(g =>
            string.IsNullOrWhiteSpace(g.Framework)
            || g.Framework.Equals("any", StringComparison.OrdinalIgnoreCase));
        if (fallbackGroup.Element is not null)
        {
            result.AddRange(ParseDependencyElements(
                fallbackGroup.Element.Elements(),
                runtimeOnly));
            return result;
        }

        throw new InvalidOperationException("互換性のある依存関係グループが見つかりませんでした。");
    }

    private static IEnumerable<PackageDependency> ParseDependencyElements(
        IEnumerable<XElement> elements,
        bool runtimeOnly)
    {
        foreach (var element in elements.Where(e => e.Name.LocalName == "dependency"))
        {
            var id = element.Attribute("id")?.Value;
            if (string.IsNullOrWhiteSpace(id)
                || runtimeOnly && !IncludesRuntimeAssets(element))
            {
                continue;
            }

            var versionText = element.Attribute("version")?.Value;
            yield return new PackageDependency(
                id,
                string.IsNullOrWhiteSpace(versionText) ? VersionRange.All : VersionRange.Parse(versionText));
        }
    }

    private static void ValidateHostPackageDependencies(
        string packagePath,
        string packageId,
        NuGetVersion packageVersion,
        IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions)
    {
        if (hostPackageVersions.Count == 0)
        {
            return;
        }

        foreach (var dependency in ReadPackageDependencies(packagePath))
        {
            if (!hostPackageVersions.TryGetValue(dependency.Id, out var hostVersion)
                || dependency.VersionRange.Satisfies(hostVersion))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"プラグイン {packageId} {packageVersion} は "
                + $"{dependency.Id} {dependency.VersionRange} を必要としますが、"
                + $"実行中のWindowTranslatorが提供するバージョンは {hostVersion} です。");
        }
    }

    private static bool IncludesRuntimeAssets(XElement dependency)
    {
        var excluded = SplitAssets(dependency.Attribute("exclude")?.Value);
        if (excluded.Contains("all") || excluded.Contains("runtime"))
        {
            return false;
        }

        var included = SplitAssets(dependency.Attribute("include")?.Value);
        return included.Count == 0 || included.Contains("all") || included.Contains("runtime");
    }

    private static HashSet<string> SplitAssets(string? assets)
        => string.IsNullOrWhiteSpace(assets)
            ? new(StringComparer.OrdinalIgnoreCase)
            : assets.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void ExtractPackageAssets(
        string packagePath,
        string destinationDirectory,
        bool requirePluginAssembly)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var libEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(e.Name)
                && e.Name != "_._")
            .ToArray();
        var libGroups = libEntries
            .Where(e => e.FullName.Split('/').Length >= 3)
            .GroupBy(e => e.FullName.Split('/')[1])
            .ToArray();
        var hasPluginAssembly = false;

        if (libGroups.Length > 0)
        {
            var selectedFramework = SelectBestTfm(libGroups.Select(g => g.Key));
            if (selectedFramework is not null)
            {
                var selectedEntries = libGroups.First(g => g.Key.Equals(
                    selectedFramework,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
                hasPluginAssembly = selectedEntries.Any(e =>
                    e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

                ExtractEntries(
                    selectedEntries,
                    $"lib/{selectedFramework}/",
                    destinationDirectory);
            }
        }

        hasPluginAssembly |= ExtractRuntimeAssets(archive, destinationDirectory);
        if (requirePluginAssembly && !hasPluginAssembly)
        {
            throw new InvalidOperationException("プラグインパッケージに互換性のあるアセンブリが見つかりませんでした。");
        }
    }

    private static bool ExtractRuntimeAssets(ZipArchive archive, string destinationDirectory)
    {
        var hasManagedAssembly = false;
        var runtimeLibIdentifier = CompatibleRuntimeIdentifiers.FirstOrDefault(rid =>
            archive.Entries.Any(e => e.FullName.StartsWith(
                $"runtimes/{rid}/lib/",
                StringComparison.OrdinalIgnoreCase)));
        var runtimeLibPrefix = $"runtimes/{runtimeLibIdentifier}/lib/";
        var runtimeLibGroups = archive.Entries
            .Where(e => runtimeLibIdentifier is not null
                && e.FullName.StartsWith(runtimeLibPrefix, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(e.Name))
            .Where(e => e.FullName.Split('/').Length >= 5)
            .GroupBy(e => e.FullName.Split('/')[3])
            .ToArray();
        if (runtimeLibGroups.Length > 0)
        {
            var selectedFramework = SelectBestTfm(runtimeLibGroups.Select(g => g.Key));
            if (selectedFramework is not null)
            {
                var selectedEntries = runtimeLibGroups.First(g => g.Key.Equals(
                    selectedFramework,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
                hasManagedAssembly = selectedEntries.Any(e =>
                    e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
                ExtractEntries(
                    selectedEntries,
                    string.Empty,
                    destinationDirectory);
            }
        }

        var nativeRuntimeIdentifier = CompatibleRuntimeIdentifiers.FirstOrDefault(rid =>
            archive.Entries.Any(e => e.FullName.StartsWith(
                $"runtimes/{rid}/native/",
                StringComparison.OrdinalIgnoreCase)));
        if (nativeRuntimeIdentifier is null)
        {
            return hasManagedAssembly;
        }

        var nativePrefix = $"runtimes/{nativeRuntimeIdentifier}/native/";
        ExtractEntries(
            archive.Entries.Where(e => e.FullName.StartsWith(nativePrefix, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(e.Name)),
            string.Empty,
            destinationDirectory);
        return hasManagedAssembly;
    }

    private static void ExtractEntries(
        IEnumerable<ZipArchiveEntry> entries,
        string prefix,
        string destinationDirectory)
    {
        foreach (var entry in entries)
        {
            var relativePath = entry.FullName[prefix.Length..];
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var destinationPath = GetSafeDestinationPath(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            if (File.Exists(destinationPath))
            {
                using var existing = File.OpenRead(destinationPath);
                using var incoming = entry.Open();
                if (!StreamsEqual(existing, incoming))
                {
                    throw new InvalidOperationException(
                        $"依存パッケージ間でファイルが競合しています: {relativePath}");
                }
                continue;
            }

            entry.ExtractToFile(destinationPath);
        }
    }

    private static string GetSafeDestinationPath(string destinationDirectory, string relativePath)
    {
        var root = Path.GetFullPath(destinationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"不正なパッケージエントリです: {relativePath}");
        }

        return destination;
    }

    private static bool StreamsEqual(Stream left, Stream right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        var leftBuffer = new byte[81920];
        var rightBuffer = new byte[81920];
        int leftRead;
        while ((leftRead = left.Read(leftBuffer, 0, leftBuffer.Length)) > 0)
        {
            var rightRead = right.Read(rightBuffer, 0, rightBuffer.Length);
            if (leftRead != rightRead
                || !leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
            {
                return false;
            }
        }

        return right.ReadByte() == -1;
    }

    private static string NormalizeFramework(string framework)
    {
        var normalized = framework.Replace(" ", string.Empty, StringComparison.Ordinal);
        const string netCoreAppPrefix = ".NETCoreApp,Version=v";
        const string netStandardPrefix = ".NETStandard,Version=v";
        if (normalized.StartsWith(netCoreAppPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"net{normalized[netCoreAppPrefix.Length..]}";
        }
        if (normalized.StartsWith(netStandardPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return $"netstandard{normalized[netStandardPrefix.Length..]}";
        }
        return normalized;
    }

    private static void ValidatePackageId(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)
            || packageId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || packageId.Contains(Path.DirectorySeparatorChar)
            || packageId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"不正なNuGetパッケージIDです: {packageId}");
        }
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
            // 一時ディレクトリの後始末失敗はインストール結果へ影響させない
        }
    }

    private sealed record PackageArtifact(string Id, NuGetVersion Version, string PackagePath);

    private sealed record PackageDependency(string Id, VersionRange VersionRange);

    private sealed record DependencyConstraint(string Source, VersionRange Range);

    private sealed record VersionIndex(
        [property: JsonPropertyName("versions")] string[]? Versions);
}
