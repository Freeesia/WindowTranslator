using System.IO;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.Catalogs;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGet経由でインストールされたプラグインを一時フォルダからロードするカタログです。
/// ファイルロックを回避するため、読み込み前にプラグインフォルダを一時フォルダにコピーします。
/// </summary>
public sealed class NuGetPluginCatalog : IPluginCatalog
{
    private static readonly string DefaultTempDir =
        Path.Combine(Path.GetTempPath(), "WindowTranslator", "plugins");

    private readonly string sourceDir;
    private readonly string tempDir;
    private readonly FolderPluginCatalog innerCatalog;

    public NuGetPluginCatalog(string sourceDir, FolderPluginCatalogOptions options)
        : this(sourceDir, DefaultTempDir, options)
    {
    }

    internal NuGetPluginCatalog(string sourceDir, string tempDir, FolderPluginCatalogOptions options)
    {
        this.sourceDir = sourceDir;
        this.tempDir = tempDir;
        this.innerCatalog = new FolderPluginCatalog(tempDir, options);
    }

    /// <inheritdoc/>
    public bool IsInitialized => this.innerCatalog.IsInitialized;

    /// <inheritdoc/>
    public async Task Initialize()
    {
        SynchronizePluginFiles(this.sourceDir, this.tempDir);

        await this.innerCatalog.Initialize().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public List<Plugin> GetPlugins() => this.innerCatalog.GetPlugins();

    /// <inheritdoc/>
    public Plugin Get(string name, Version version) => this.innerCatalog.Get(name, version);

    internal static void SynchronizePluginFiles(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        var sourceFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sourceDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(source))
        {
            CollectSourceEntries(
                source,
                source,
                isRoot: true,
                sourceFiles,
                sourceDirectories);
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

    private static bool IsWorkingDirectory(string directoryName)
        => directoryName.EndsWith(".backup", StringComparison.OrdinalIgnoreCase)
        || directoryName.Contains(".backup-", StringComparison.OrdinalIgnoreCase)
        || directoryName.Contains(".uninstalling-", StringComparison.OrdinalIgnoreCase)
        || directoryName.Contains(".installing-", StringComparison.OrdinalIgnoreCase);

    private static bool IsManagementFile(string fileName)
        => fileName.Equals("nuget-manifest.json", StringComparison.OrdinalIgnoreCase)
        || fileName.StartsWith("nuget-manifest.json.tmp-", StringComparison.OrdinalIgnoreCase);

    private static void CollectSourceEntries(
        string sourceRoot,
        string currentDirectory,
        bool isRoot,
        Dictionary<string, string> sourceFiles,
        HashSet<string> sourceDirectories)
    {
        foreach (var file in Directory.EnumerateFiles(currentDirectory))
        {
            if (isRoot && IsManagementFile(Path.GetFileName(file)))
            {
                continue;
            }

            sourceFiles[Path.GetRelativePath(sourceRoot, file)] = file;
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(currentDirectory))
        {
            if (isRoot && IsWorkingDirectory(Path.GetFileName(subDirectory)))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(sourceRoot, subDirectory);
            sourceDirectories.Add(relativePath);
            CollectSourceEntries(
                sourceRoot,
                subDirectory,
                isRoot: false,
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
}
