using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGetパッケージとそのランタイム依存関係を、プラグインフォルダへ展開します。
/// </summary>
internal sealed class NuGetPackageInstaller(
    FindPackageByIdResource packageResource,
    ILogger logger,
    IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions)
{
    private static readonly NuGetFramework HostFramework = GetHostFramework();
    private static readonly FrameworkReducer FrameworkReducer = new();

    private static readonly string[] CompatibleRuntimeIdentifiers =
        [RuntimeInformation.RuntimeIdentifier, "win", "any"];

    private readonly FindPackageByIdResource packageResource = packageResource;
    private readonly ILogger logger = logger;
    private readonly IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions = hostPackageVersions;

    public async Task<VersionRange> InstallAsync(
        string packageId,
        string version,
        string destinationDirectory,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        PackageIdValidator.ValidatePackageId(packageId);
        var requestedVersion = NuGetVersion.Parse(version);
        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "WindowTranslatorPlugins",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        progress?.Report(0);

        try
        {
            var artifacts = await ResolvePackageGraphAsync(
                packageId,
                requestedVersion,
                workDirectory,
                progress,
                cancellationToken).ConfigureAwait(false);
            progress?.Report(60);

            Directory.CreateDirectory(destinationDirectory);
            var orderedArtifacts = artifacts.OrderByDescending(a =>
                a.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)).ToArray();
            for (var index = 0; index < orderedArtifacts.Length; index++)
            {
                var artifact = orderedArtifacts[index];
                ExtractPackageAssets(
                    artifact.PackagePath,
                    destinationDirectory,
                    requirePluginAssembly: artifact.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
                progress?.Report(60 + (30d * (index + 1) / orderedArtifacts.Length));
            }

            var rootPackage = artifacts.First(artifact =>
                artifact.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            return rootPackage.Metadata.Dependencies.First(dependency =>
                dependency.Id.Equals(
                    NuGetPluginService.AbstractionsPackageId,
                    StringComparison.OrdinalIgnoreCase)).VersionRange;
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
            .Select(original => (Original: original, Framework: ParseFramework(original)))
            .Where(candidate => candidate.Framework is not null)
            .ToArray();

        var nearest = FrameworkReducer.GetNearest(
            HostFramework,
            candidates.Select(candidate => candidate.Framework!));
        if (nearest is null)
        {
            return null;
        }

        return candidates.First(candidate => NuGetFrameworkFullComparer.Instance.Equals(
            candidate.Framework,
            nearest)).Original;
    }

    internal static PackageDependencyGroup? SelectBestDependencyGroup(
        IEnumerable<PackageDependencyGroup> dependencyGroups)
    {
        var groups = dependencyGroups.ToArray();
        var frameworkGroups = groups
            .Where(group => !group.TargetFramework.IsAny && !group.TargetFramework.IsUnsupported)
            .ToArray();
        var nearest = FrameworkReducer.GetNearest(
            HostFramework,
            frameworkGroups.Select(group => group.TargetFramework));
        if (nearest is not null)
        {
            return frameworkGroups.First(group => NuGetFrameworkFullComparer.Instance.Equals(
                group.TargetFramework,
                nearest));
        }

        return groups.FirstOrDefault(group => group.TargetFramework.IsAny);
    }

    private static NuGetFramework GetHostFramework()
    {
        var assembly = typeof(NuGetPackageInstaller).Assembly;
        var frameworkName = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName
            ?? throw new InvalidOperationException("WindowTranslator のターゲットフレームワークを取得できませんでした。");
        var framework = NuGetFramework.Parse(frameworkName);
        var platformName = assembly.GetCustomAttribute<TargetPlatformAttribute>()?.PlatformName;
        return string.IsNullOrWhiteSpace(platformName)
            ? framework
            : NuGetFramework.ParseFolder($"{framework.GetShortFolderName()}-{platformName}");
    }

    private static NuGetFramework? ParseFramework(string framework)
    {
        if (string.IsNullOrWhiteSpace(framework))
        {
            return null;
        }

        var parsed = NuGetFramework.Parse(framework);
        return parsed.IsUnsupported ? null : parsed;
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
        using var cacheContext = new SourceCacheContext();

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
                : await ResolveDependencyVersionAsync(
                    currentId,
                    ranges,
                    cacheContext,
                    cancellationToken).ConfigureAwait(false);

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
                cacheContext,
                cancellationToken).ConfigureAwait(false);
            var metadata = ReadPackageMetadata(packagePath);
            ValidateHostPackageDependencies(
                metadata,
                currentId,
                resolvedVersion,
                this.hostPackageVersions,
                requirePluginPackage: currentId.Equals(rootPackageId, StringComparison.OrdinalIgnoreCase));

            artifacts[currentId] = new PackageArtifact(currentId, packagePath, metadata);
            foreach (var dependency in metadata.Dependencies.Where(IncludesRuntimeAssets))
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
            PackageIdValidator.ValidatePackageId(id);
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
        SourceCacheContext cacheContext,
        CancellationToken cancellationToken)
    {
        var versions = await this.packageResource.GetAllVersionsAsync(
            packageId,
            cacheContext,
            NuGet.Common.NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);
        var compatibleVersions = versions
            .Where(v => ranges.All(r => r.Satisfies(v)))
            .OrderBy(v => v)
            .ToArray();
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
        SourceCacheContext cacheContext,
        CancellationToken cancellationToken)
    {
        this.logger.LogInformation("NuGetパッケージをダウンロード中: {PackageId} {Version}", packageId, version);
        progress?.Report(10);
        await using var destination = File.Create(destinationPath);
        var copied = await this.packageResource.CopyNupkgToStreamAsync(
            packageId,
            version,
            destination,
            cacheContext,
            NuGet.Common.NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);
        if (!copied)
        {
            throw new InvalidOperationException($"NuGetパッケージを取得できませんでした: {packageId} {version}");
        }
        progress?.Report(50);
    }

    private static PackageMetadata ReadPackageMetadata(string packagePath)
    {
        using var packageStream = File.OpenRead(packagePath);
        using var packageReader = new PackageArchiveReader(packageStream);
        var groups = packageReader.NuspecReader.GetDependencyGroups().ToArray();
        var selectedGroup = SelectBestDependencyGroup(groups);
        if (selectedGroup is null && groups.Length > 0)
        {
            throw new InvalidOperationException("互換性のある依存関係グループが見つかりませんでした。");
        }

        var dependencies = new List<PackageDependency>();
        var anyGroup = groups.FirstOrDefault(group => group.TargetFramework.IsAny);
        if (anyGroup is not null && !ReferenceEquals(anyGroup, selectedGroup))
        {
            dependencies.AddRange(anyGroup.Packages);
        }
        if (selectedGroup is not null)
        {
            dependencies.AddRange(selectedGroup.Packages);
        }

        var tags = packageReader.NuspecReader.GetTags();
        var hasPluginTag = tags?.Split(
                [' ', '\t', '\r', '\n', ';', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(NuGetPluginService.PluginTag, StringComparer.OrdinalIgnoreCase) == true;
        return new(dependencies, hasPluginTag);
    }

    private static void ValidateHostPackageDependencies(
        PackageMetadata metadata,
        string packageId,
        NuGetVersion packageVersion,
        IReadOnlyDictionary<string, NuGetVersion> hostPackageVersions,
        bool requirePluginPackage)
    {
        if (requirePluginPackage)
        {
            if (!metadata.HasPluginTag)
            {
                throw new InvalidOperationException(
                    $"パッケージ {packageId} {packageVersion} はWindowTranslatorプラグインタグを持っていません。");
            }

            if (!PluginCompatibility.ValidationDisabled
                && !hostPackageVersions.ContainsKey(NuGetPluginService.AbstractionsPackageId))
            {
                throw new InvalidOperationException(
                    $"実行中の{NuGetPluginService.AbstractionsPackageId}のバージョンを確認できません。");
            }

            if (!metadata.Dependencies.Any(dependency => dependency.Id.Equals(
                    NuGetPluginService.AbstractionsPackageId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"パッケージ {packageId} {packageVersion} は"
                    + $"{NuGetPluginService.AbstractionsPackageId}へ直接依存していません。");
            }
        }

        foreach (var dependency in metadata.Dependencies)
        {
            if (!hostPackageVersions.TryGetValue(dependency.Id, out var hostVersion)
                || PluginCompatibility.IsVersionCompatible(dependency.VersionRange, hostVersion))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"プラグイン {packageId} {packageVersion} は "
                + $"{dependency.Id} {dependency.VersionRange} を必要としますが、"
                + $"実行中のWindowTranslatorが提供するバージョンは {hostVersion} です。");
        }
    }

    private static bool IncludesRuntimeAssets(PackageDependency dependency)
    {
        if (dependency.Exclude.Contains("all", StringComparer.OrdinalIgnoreCase)
            || dependency.Exclude.Contains("runtime", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return dependency.Include.Count == 0
            || dependency.Include.Contains("all", StringComparer.OrdinalIgnoreCase)
            || dependency.Include.Contains("runtime", StringComparer.OrdinalIgnoreCase);
    }

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
        var hasPluginAssembly = ExtractRuntimeAssets(archive, destinationDirectory);

        if (!hasPluginAssembly && libGroups.Length > 0)
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
                    $"{runtimeLibPrefix}{selectedFramework}/",
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
            nativePrefix,
            destinationDirectory,
            flatten: true);
        return hasManagedAssembly;
    }

    private static void ExtractEntries(
        IEnumerable<ZipArchiveEntry> entries,
        string prefix,
        string destinationDirectory,
        bool flatten = false)
    {
        foreach (var entry in entries)
        {
            var relativePath = flatten ? entry.Name : entry.FullName[prefix.Length..];
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

    private sealed record PackageArtifact(
        string Id,
        string PackagePath,
        PackageMetadata Metadata);

    private sealed record DependencyConstraint(string Source, VersionRange Range);

    private sealed record PackageMetadata(
        IReadOnlyList<PackageDependency> Dependencies,
        bool HasPluginTag);

}
