using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.Catalogs;
using Weikio.PluginFramework.Context;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGet経由でインストールされたプラグインを一時フォルダからロードするカタログです。
/// ファイルロックを回避するため、読み込み前にプラグインフォルダを一時フォルダにコピーします。
/// </summary>
public sealed class NuGetPluginCatalog : IPluginCatalog
{
    private static readonly string DefaultTempDir =
        Path.Combine(Path.GetTempPath(), "WindowTranslator", "nuget-plugins");

    private readonly string sourceDir;
    private readonly string tempDir;
    private readonly int hostMajorVersion;
    private readonly FolderPluginCatalogOptions options;
    private CompositePluginCatalog innerCatalog = new();

    public NuGetPluginCatalog(
        string sourceDir,
        int hostMajorVersion,
        FolderPluginCatalogOptions options)
        : this(sourceDir, DefaultTempDir, hostMajorVersion, options)
    {
    }

    internal NuGetPluginCatalog(
        string sourceDir,
        string tempDir,
        int hostMajorVersion,
        FolderPluginCatalogOptions options)
    {
        this.sourceDir = sourceDir;
        this.tempDir = tempDir;
        this.hostMajorVersion = hostMajorVersion;
        this.options = options;
    }

    /// <inheritdoc/>
    public bool IsInitialized => this.innerCatalog.IsInitialized;

    /// <inheritdoc/>
    public async Task Initialize()
    {
        var unresolvedOperations = await NuGetPluginOperation
            .RecoverInterruptedOperationsAsync(this.sourceDir)
            .ConfigureAwait(false);
        var loadablePackages = GetLoadablePackageIds(
            this.sourceDir,
            this.hostMajorVersion);
        loadablePackages.ExceptWith(unresolvedOperations);
        SynchronizePluginFiles(this.sourceDir, this.tempDir, loadablePackages);

        this.innerCatalog = CreateCatalog(this.tempDir, this.options);
        await this.innerCatalog.Initialize().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public List<Plugin> GetPlugins() => this.innerCatalog.GetPlugins();

    /// <inheritdoc/>
    public Plugin Get(string name, Version version) => this.innerCatalog.Get(name, version);

    private static CompositePluginCatalog CreateCatalog(
        string directory,
        FolderPluginCatalogOptions baseOptions)
    {
        return new CompositePluginCatalog([.. Directory
            .EnumerateDirectories(directory)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(packageDirectory => new FolderPluginCatalog(
                packageDirectory,
                CreateCatalogOptions(baseOptions, packageDirectory))) ]);
    }

    private static FolderPluginCatalogOptions CreateCatalogOptions(
        FolderPluginCatalogOptions baseOptions,
        string pluginDirectory)
    {
        var baseLoadOptions = baseOptions.PluginLoadContextOptions;
        var files = Directory
            .EnumerateFiles(pluginDirectory, "*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Select(path => CreatePluginFile(pluginDirectory, path))
            .OrderBy(file => GetRuntimeAssetPriority(file.RelativePath))
            .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var satelliteAssemblies = files
            .Where(file => file.IsSatelliteAssembly)
            .GroupBy(file => GetSatelliteKey(
                file.AssemblyName!.Name!,
                file.AssemblyName.CultureName!))
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        var runtimeHints = new List<RuntimeAssemblyHint>(
            baseLoadOptions.RuntimeAssemblyHints ?? []);
        var hintKeys = runtimeHints
            .Select(hint => GetHintKey(hint.FileName, hint.IsNative))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files
                     .Where(file => !file.IsSatelliteAssembly)
                     .OrderByDescending(file => file.IsManaged))
        {
            var isNative = !file.IsManaged;
            if (hintKeys.Add(GetHintKey(Path.GetFileName(file.Path), isNative)))
            {
                runtimeHints.Add(new RuntimeAssemblyHint(
                    Path.GetFileName(file.Path),
                    file.Path,
                    isNative));
            }
        }

        var additionalRuntimePaths = new List<string>(
            baseLoadOptions.AdditionalRuntimePaths ?? []);
        foreach (var path in files
                     .Where(file => file.IsManaged && !file.IsSatelliteAssembly)
                     .Select(file => Path.GetDirectoryName(file.Path)!)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!additionalRuntimePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                additionalRuntimePaths.Add(path);
            }
        }

        return new FolderPluginCatalogOptions
        {
            IncludeSubfolders = true,
            SearchPatterns = [.. baseOptions.SearchPatterns],
            TypeFinderOptions = baseOptions.TypeFinderOptions,
            PluginNameOptions = CreatePluginNameOptions(
                baseOptions.PluginNameOptions,
                satelliteAssemblies),
            PluginLoadContextOptions = new PluginLoadContextOptions
            {
                UseHostApplicationAssemblies = baseLoadOptions.UseHostApplicationAssemblies,
                HostApplicationAssemblies = [.. baseLoadOptions.HostApplicationAssemblies],
                LoggerFactory = baseLoadOptions.LoggerFactory,
                AdditionalRuntimePaths = additionalRuntimePaths,
                RuntimeAssemblyHints = runtimeHints,
            },
        };
    }

    private static PluginFile CreatePluginFile(string pluginDirectory, string path)
    {
        var assemblyName = TryGetAssemblyName(path);
        return new PluginFile(
            path,
            Path.GetRelativePath(pluginDirectory, path),
            assemblyName is not null,
            assemblyName);
    }

    private static PluginNameOptions CreatePluginNameOptions(
        PluginNameOptions baseOptions,
        IReadOnlyDictionary<string, PluginFile> satelliteAssemblies)
    {
        if (satelliteAssemblies.Count == 0)
        {
            return baseOptions;
        }

        var configuredContexts = new HashSet<AssemblyLoadContext>();
        var contextLock = new object();

        void EnsureSatelliteResolver(Type type)
        {
            var context = AssemblyLoadContext.GetLoadContext(type.Assembly);
            if (context is null || context == AssemblyLoadContext.Default)
            {
                return;
            }

            lock (contextLock)
            {
                if (configuredContexts.Add(context))
                {
                    context.Resolving += ResolveSatelliteAssembly;
                }
            }
        }

        Assembly? ResolveSatelliteAssembly(
            AssemblyLoadContext context,
            AssemblyName requestedAssembly)
        {
            if (string.IsNullOrWhiteSpace(requestedAssembly.Name)
                || string.IsNullOrWhiteSpace(requestedAssembly.CultureName)
                || !satelliteAssemblies.TryGetValue(
                    GetSatelliteKey(requestedAssembly.Name, requestedAssembly.CultureName),
                    out var satelliteAssembly)
                || requestedAssembly.Version is not null
                    && satelliteAssembly.AssemblyName!.Version != requestedAssembly.Version)
            {
                return null;
            }

            return context.LoadFromAssemblyPath(satelliteAssembly.Path);
        }

        return new PluginNameOptions
        {
            PluginNameGenerator = (_, type) =>
            {
                EnsureSatelliteResolver(type);
                return baseOptions.PluginNameGenerator(baseOptions, type);
            },
            PluginVersionGenerator = (_, type) =>
            {
                EnsureSatelliteResolver(type);
                return baseOptions.PluginVersionGenerator(baseOptions, type);
            },
            PluginDescriptionGenerator = (_, type) =>
            {
                EnsureSatelliteResolver(type);
                return baseOptions.PluginDescriptionGenerator(baseOptions, type);
            },
            PluginProductVersionGenerator = (_, type) =>
            {
                EnsureSatelliteResolver(type);
                return baseOptions.PluginProductVersionGenerator(baseOptions, type);
            },
        };
    }

    private static AssemblyName? TryGetAssemblyName(string path)
    {
        try
        {
            return AssemblyName.GetAssemblyName(path);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (FileLoadException)
        {
            return null;
        }
    }

    private static int GetRuntimeAssetPriority(string relativePath)
        => relativePath.StartsWith(
            $"runtimes{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase)
            ? 0
            : 1;

    private static string GetHintKey(string fileName, bool isNative)
        => $"{(isNative ? 'N' : 'M')}:{fileName}";

    private static string GetSatelliteKey(string assemblyName, string cultureName)
        => $"{cultureName}:{assemblyName}";

    internal static void SynchronizePluginFiles(
        string source,
        string destination,
        IReadOnlySet<string> includedRootDirectories)
    {
        Directory.CreateDirectory(destination);

        var sourceFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sourceDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(source))
        {
            foreach (var packageDirectory in Directory.EnumerateDirectories(source)
                         .Where(path => includedRootDirectories.Contains(Path.GetFileName(path))))
            {
                sourceDirectories.Add(Path.GetRelativePath(source, packageDirectory));
                CollectSourceEntries(
                    source,
                    packageDirectory,
                    sourceFiles,
                    sourceDirectories);
            }
        }

        foreach (var relativeDirectory in sourceDirectories.OrderBy(GetPathDepth))
        {
            var destinationDirectory = Path.Combine(destination, relativeDirectory);
            if (File.Exists(destinationDirectory))
            {
                File.Delete(destinationDirectory);
            }

            Directory.CreateDirectory(destinationDirectory);
        }

        foreach (var (relativePath, sourceFile) in sourceFiles)
        {
            var destinationFile = Path.Combine(destination, relativePath);
            if (FilesMatch(sourceFile, destinationFile))
            {
                continue;
            }

            if (Directory.Exists(destinationFile))
            {
                Directory.Delete(destinationFile, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            CopyFile(sourceFile, destinationFile);
        }

        foreach (var destinationFile in Directory.EnumerateFiles(
                     destination,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(destination, destinationFile);
            if (!sourceFiles.ContainsKey(relativePath))
            {
                File.Delete(destinationFile);
            }
        }

        foreach (var destinationDirectory in Directory
                     .EnumerateDirectories(destination, "*", SearchOption.AllDirectories)
                     .OrderByDescending(GetPathDepth))
        {
            var relativePath = Path.GetRelativePath(destination, destinationDirectory);
            if (!sourceDirectories.Contains(relativePath))
            {
                Directory.Delete(destinationDirectory, recursive: true);
            }
        }
    }

    internal static HashSet<string> GetLoadablePackageIds(
        string sourceDirectory,
        int hostMajorVersion)
    {
        try
        {
            using var stream = File.OpenRead(Path.Combine(
                sourceDirectory,
                "nuget-manifest.json"));
            var packages = JsonSerializer.Deserialize<InstalledManifest>(
                stream,
                NuGetPluginService.ManifestJsonOptions)?.Packages
                ?? throw new InvalidDataException("プラグインmanifestにパッケージ一覧がありません。");
            return packages
                .Where(package => PluginCompatibility.IsHostMajorCompatible(
                    package.HostMajorVersion,
                    hostMajorVersion))
                .Select(package => package.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void CollectSourceEntries(
        string sourceRoot,
        string currentDirectory,
        Dictionary<string, string> sourceFiles,
        HashSet<string> sourceDirectories)
    {
        foreach (var file in Directory.EnumerateFiles(currentDirectory))
        {
            sourceFiles[Path.GetRelativePath(sourceRoot, file)] = file;
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(currentDirectory))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, subDirectory);
            sourceDirectories.Add(relativePath);
            CollectSourceEntries(
                sourceRoot,
                subDirectory,
                sourceFiles,
                sourceDirectories);
        }
    }

    private static bool FilesMatch(string source, string destination)
    {
        if (!File.Exists(destination))
        {
            return false;
        }

        var sourceInfo = new FileInfo(source);
        var destinationInfo = new FileInfo(destination);
        return sourceInfo.Length == destinationInfo.Length
            && sourceInfo.LastWriteTimeUtc == destinationInfo.LastWriteTimeUtc
            && sourceInfo.CreationTimeUtc == destinationInfo.CreationTimeUtc;
    }

    private static void CopyFile(string source, string destination)
    {
        var temporaryPath = $"{destination}.sync-{Guid.NewGuid():N}";
        try
        {
            File.Copy(source, temporaryPath);
            File.SetCreationTimeUtc(temporaryPath, File.GetCreationTimeUtc(source));
            File.SetLastWriteTimeUtc(temporaryPath, File.GetLastWriteTimeUtc(source));
            File.Move(temporaryPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static int GetPathDepth(string path)
        => path.Count(character =>
            character == Path.DirectorySeparatorChar
            || character == Path.AltDirectorySeparatorChar);

    private sealed record PluginFile(
        string Path,
        string RelativePath,
        bool IsManaged,
        AssemblyName? AssemblyName)
    {
        public bool IsSatelliteAssembly
            => !string.IsNullOrWhiteSpace(this.AssemblyName?.CultureName);
    }
}
