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
    private static readonly string TempDir =
        Path.Combine(Path.GetTempPath(), "WindowTranslator", "plugins");

    private readonly string sourceDir;
    private readonly FolderPluginCatalog innerCatalog;

    public NuGetPluginCatalog(string sourceDir, FolderPluginCatalogOptions options)
    {
        this.sourceDir = sourceDir;
        this.innerCatalog = new FolderPluginCatalog(TempDir, options);
    }

    /// <inheritdoc/>
    public bool IsInitialized => this.innerCatalog.IsInitialized;

    /// <inheritdoc/>
    public async Task Initialize()
    {
        // ロック解除のために一時フォルダを削除してからコピー
        if (Directory.Exists(TempDir))
        {
            Directory.Delete(TempDir, recursive: true);
        }
        Directory.CreateDirectory(TempDir);

        if (Directory.Exists(this.sourceDir))
        {
            // プラグインのサブフォルダのみコピー（nuget-manifest.json等のファイルはスキップ）
            foreach (var subDir in Directory.GetDirectories(this.sourceDir))
            {
                var destSubDir = Path.Combine(TempDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        await this.innerCatalog.Initialize().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public List<Plugin> GetPlugins() => this.innerCatalog.GetPlugins();

    /// <inheritdoc/>
    public Plugin Get(string name, Version version) => this.innerCatalog.Get(name, version);

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            var destinationFile = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(destinationFile))
            {
                continue;
            }

            File.Copy(file, destinationFile);
        }
        foreach (var subDir in Directory.GetDirectories(source))
        {
            CopyDirectory(subDir, Path.Combine(destination, Path.GetFileName(subDir)));
        }
    }
}
