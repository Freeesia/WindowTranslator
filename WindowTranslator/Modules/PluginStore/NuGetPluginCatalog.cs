using System.IO;
using Weikio.PluginFramework.Catalogs;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// NuGet経由でインストールされたプラグインを一時フォルダからロードするカタログです。
/// ファイルロックを回避するため、読み込み前にプラグインフォルダを一時フォルダにコピーします。
/// </summary>
public class NuGetPluginCatalog(string sourceDir, FolderPluginCatalogOptions options)
    : FolderPluginCatalog(
        Path.Combine(Path.GetTempPath(), "WindowTranslator", "plugins"),
        options)
{
    private static readonly string TempDir =
        Path.Combine(Path.GetTempPath(), "WindowTranslator", "plugins");

    /// <inheritdoc/>
    public override async Task Initialize()
    {
        // ロック解除のために一時フォルダを削除してからコピー
        if (Directory.Exists(TempDir))
        {
            Directory.Delete(TempDir, recursive: true);
        }
        Directory.CreateDirectory(TempDir);

        if (Directory.Exists(sourceDir))
        {
            // プラグインのサブフォルダのみコピー（nuget-manifest.json等のファイルはスキップ）
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(TempDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        await base.Initialize().ConfigureAwait(false);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
        foreach (var subDir in Directory.GetDirectories(source))
        {
            CopyDirectory(subDir, Path.Combine(destination, Path.GetFileName(subDir)));
        }
    }
}
