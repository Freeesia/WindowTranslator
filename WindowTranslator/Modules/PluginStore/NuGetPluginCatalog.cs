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
        // ロック解除のために一時フォルダを削除してからコピー
        if (Directory.Exists(this.tempDir))
        {
            Directory.Delete(this.tempDir, recursive: true);
        }
        Directory.CreateDirectory(this.tempDir);

        if (Directory.Exists(this.sourceDir))
        {
            CopyPluginFiles(this.sourceDir, this.tempDir);
        }

        await this.innerCatalog.Initialize().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public List<Plugin> GetPlugins() => this.innerCatalog.GetPlugins();

    /// <inheritdoc/>
    public Plugin Get(string name, Version version) => this.innerCatalog.Get(name, version);

    internal static void CopyPluginFiles(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var fileName = Path.GetFileName(file);
            if (IsManagementFile(fileName))
            {
                continue;
            }

            var destinationFile = Path.Combine(destination, fileName);
            if (!File.Exists(destinationFile))
            {
                File.Copy(file, destinationFile);
            }
        }

        foreach (var subDir in Directory.GetDirectories(source))
        {
            var directoryName = Path.GetFileName(subDir);
            if (IsWorkingDirectory(directoryName))
            {
                continue;
            }

            CopyDirectory(subDir, Path.Combine(destination, directoryName));
        }
    }

    private static bool IsWorkingDirectory(string directoryName)
        => directoryName.EndsWith(".backup", StringComparison.OrdinalIgnoreCase)
        || directoryName.Contains(".backup-", StringComparison.OrdinalIgnoreCase)
        || directoryName.Contains(".installing-", StringComparison.OrdinalIgnoreCase);

    private static bool IsManagementFile(string fileName)
        => fileName.Equals("nuget-manifest.json", StringComparison.OrdinalIgnoreCase)
        || fileName.StartsWith("nuget-manifest.json.tmp-", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".pending-delete", StringComparison.OrdinalIgnoreCase)
        || fileName.Contains(".pending-delete.tmp-", StringComparison.OrdinalIgnoreCase);

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            var destinationFile = Path.Combine(destination, Path.GetFileName(file));
            if (!File.Exists(destinationFile))
            {
                File.Copy(file, destinationFile);
            }
        }
        foreach (var subDir in Directory.GetDirectories(source))
        {
            CopyDirectory(subDir, Path.Combine(destination, Path.GetFileName(subDir)));
        }
    }
}
