using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace WindowTranslator.Modules.PluginStore;

internal enum NuGetPluginOperationKind
{
    Install,
    Uninstall,
}

internal sealed record NuGetPluginOperationState(
    string OperationId,
    string PackageId,
    NuGetPluginOperationKind Kind,
    bool ManifestExisted,
    InstalledManifest OriginalManifest);

internal sealed record NuGetPluginOperationPaths(
    string OperationId,
    string PackageId,
    string JournalPath,
    string CommittedPath,
    string StagingPath,
    string BackupPath,
    string UninstallingPath);

internal static class NuGetPluginOperation
{
    private const string JournalSuffix = ".operation.json";
    private const string CommittedSuffix = ".committed";

    internal static NuGetPluginOperationPaths CreatePaths(string nugetPluginsDir, string packageId)
        => GetPaths(nugetPluginsDir, packageId, Guid.NewGuid().ToString("N"));

    internal static string GetPackageDirectory(string nugetPluginsDir, string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)
            || packageId is "." or ".."
            || packageId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || packageId.Contains(Path.DirectorySeparatorChar)
            || packageId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"不正なNuGetパッケージIDです: {packageId}");
        }

        var root = Path.GetFullPath(nugetPluginsDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var packageDirectory = Path.GetFullPath(Path.Combine(root, packageId));
        if (!packageDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"不正なNuGetパッケージIDです: {packageId}");
        }

        return packageDirectory;
    }

    internal static Task WriteJournalAsync(
        NuGetPluginOperationPaths paths,
        NuGetPluginOperationState state,
        CancellationToken cancellationToken)
    {
        if (!paths.OperationId.Equals(state.OperationId, StringComparison.Ordinal)
            || !paths.PackageId.Equals(state.PackageId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("NuGetプラグイン操作とジャーナルの対象が一致しません。");
        }

        return SaveJsonAsync(paths.JournalPath, state, cancellationToken);
    }

    internal static void MarkCommitted(NuGetPluginOperationPaths paths)
    {
        using var stream = new FileStream(
            paths.CommittedPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.WriteThrough);
        stream.Flush(flushToDisk: true);
    }

    internal static async Task<IReadOnlySet<string>> RecoverInterruptedOperationsAsync(
        string nugetPluginsDir,
        CancellationToken cancellationToken = default)
    {
        var operationsDir = Path.Combine(
            Path.GetFullPath(nugetPluginsDir),
            NuGetPluginService.OperationsDirectoryName);
        if (!Directory.Exists(operationsDir))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var unresolvedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var journalPath in Directory.EnumerateFiles(
                     operationsDir,
                     $"*{JournalSuffix}",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            NuGetPluginOperationState? state = null;
            try
            {
                await using (var stream = File.OpenRead(journalPath))
                {
                    state = await JsonSerializer.DeserializeAsync<NuGetPluginOperationState>(
                        stream,
                        NuGetPluginService.ManifestJsonOptions,
                        cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidDataException("NuGetプラグイン操作ジャーナルが空です。");
                }
                var paths = GetPaths(nugetPluginsDir, state.PackageId, state.OperationId);
                if (!paths.JournalPath.Equals(journalPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("NuGetプラグイン操作ジャーナルのIDが一致しません。");
                }

                if (File.Exists(paths.CommittedPath))
                {
                    CleanupCommitted(paths);
                    continue;
                }

                await RollbackAsync(
                    nugetPluginsDir,
                    state,
                    paths,
                    cancellationToken).ConfigureAwait(false);
                CleanupRolledBack(paths);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!string.IsNullOrWhiteSpace(state?.PackageId))
                {
                    unresolvedPackageIds.Add(state.PackageId);
                }
                Trace.TraceWarning(
                    "NuGetプラグイン操作の復旧に失敗しました: {0} ({1})",
                    journalPath,
                    ex);
            }
        }

        return unresolvedPackageIds;
    }

    internal static async Task RollbackAsync(
        string nugetPluginsDir,
        NuGetPluginOperationState state,
        NuGetPluginOperationPaths paths,
        CancellationToken cancellationToken)
    {
        var targetDir = GetPackageDirectory(nugetPluginsDir, state.PackageId);
        switch (state.Kind)
        {
            case NuGetPluginOperationKind.Install:
                if (!Directory.Exists(paths.StagingPath) && Directory.Exists(targetDir))
                {
                    Directory.Move(targetDir, paths.StagingPath);
                }
                if (Directory.Exists(paths.BackupPath))
                {
                    Directory.Move(paths.BackupPath, targetDir);
                }
                break;
            case NuGetPluginOperationKind.Uninstall:
                if (Directory.Exists(paths.UninstallingPath) && !Directory.Exists(targetDir))
                {
                    Directory.Move(paths.UninstallingPath, targetDir);
                }
                break;
            default:
                throw new InvalidDataException($"不明なNuGetプラグイン操作です: {state.Kind}");
        }

        var manifestPath = Path.Combine(Path.GetFullPath(nugetPluginsDir), "nuget-manifest.json");
        if (state.ManifestExisted)
        {
            await SaveManifestAsync(
                manifestPath,
                state.OriginalManifest,
                cancellationToken).ConfigureAwait(false);
        }
        else if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
        }
    }

    internal static void CleanupCommitted(NuGetPluginOperationPaths paths)
    {
        DeleteDirectoryIfExists(paths.StagingPath);
        DeleteDirectoryIfExists(paths.BackupPath);
        DeleteDirectoryIfExists(paths.UninstallingPath);
        File.Delete(paths.JournalPath);
        File.Delete(paths.CommittedPath);
    }

    internal static void CleanupRolledBack(NuGetPluginOperationPaths paths)
    {
        File.Delete(paths.JournalPath);
        DeleteDirectoryIfExists(paths.StagingPath);
        DeleteDirectoryIfExists(paths.BackupPath);
        DeleteDirectoryIfExists(paths.UninstallingPath);
        File.Delete(paths.CommittedPath);
    }

    internal static Task SaveManifestAsync(
        string manifestPath,
        InstalledManifest manifest,
        CancellationToken cancellationToken)
        => SaveJsonAsync(manifestPath, manifest, cancellationToken);

    private static NuGetPluginOperationPaths GetPaths(
        string nugetPluginsDir,
        string packageId,
        string operationId)
    {
        if (!Guid.TryParseExact(operationId, "N", out _))
        {
            throw new InvalidOperationException($"不正なNuGetプラグイン操作IDです: {operationId}");
        }
        _ = GetPackageDirectory(nugetPluginsDir, packageId);

        var operationsDir = Path.Combine(
            Path.GetFullPath(nugetPluginsDir),
            NuGetPluginService.OperationsDirectoryName);
        return new(
            operationId,
            packageId,
            Path.Combine(operationsDir, $"{operationId}{JournalSuffix}"),
            Path.Combine(operationsDir, $"{operationId}{CommittedSuffix}"),
            Path.Combine(operationsDir, $"{packageId}.installing-{operationId}"),
            Path.Combine(operationsDir, $"{packageId}.backup-{operationId}"),
            Path.Combine(operationsDir, $"{packageId}.uninstalling-{operationId}"));
    }

    private static async Task SaveJsonAsync<T>(
        string destinationPath,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var temporaryPath = $"{destinationPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    value,
                    NuGetPluginService.ManifestJsonOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
