using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Packaging;

namespace WindowTranslator.Modules.PluginStore;

internal sealed record NuGetPluginOperationState(
    [property: JsonRequired] string PackageId,
    InstalledManifest? OriginalManifest);

internal sealed class NuGetPluginOperation : IAsyncDisposable
{
    internal const string OperationsDirectoryName = ".operations";
    private const string PendingFileName = "pending.json";
    private const string CommittedFileName = "committed.json";

    private readonly string rootDirectory;
    private readonly string operationDirectory;
    private readonly NuGetPluginOperationState state;
    private bool committed;

    private NuGetPluginOperation(
        string rootDirectory,
        string operationDirectory,
        NuGetPluginOperationState state)
    {
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        this.operationDirectory = Path.GetFullPath(operationDirectory);
        this.state = state;
        _ = this.TargetPath;
    }

    internal string TargetPath => GetPackageDirectory(this.rootDirectory, this.state.PackageId);

    internal string WorkingPath => Path.Combine(this.operationDirectory, "working");

    internal string BackupPath => Path.Combine(this.operationDirectory, "backup");

    internal string PendingPath => Path.Combine(this.operationDirectory, PendingFileName);

    internal string CommittedPath => Path.Combine(this.operationDirectory, CommittedFileName);

    internal static async Task<NuGetPluginOperation> BeginAsync(
        string rootDirectory,
        string packageId,
        InstalledManifest? originalManifest,
        CancellationToken cancellationToken)
    {
        var operationDirectory = Path.Combine(
            Path.GetFullPath(rootDirectory),
            OperationsDirectoryName,
            Guid.NewGuid().ToString("N"));
        var operation = new NuGetPluginOperation(
            rootDirectory,
            operationDirectory,
            new(packageId, originalManifest));
        Directory.CreateDirectory(operationDirectory);
        Directory.CreateDirectory(operation.WorkingPath);

        try
        {
            await SaveJsonAsync(
                operation.PendingPath,
                operation.state,
                cancellationToken).ConfigureAwait(false);
            return operation;
        }
        catch
        {
            DeleteDirectoryIfExists(operationDirectory);
            throw;
        }
    }

    internal static string GetPackageDirectory(string rootDirectory, string packageId)
    {
        PackageIdValidator.ValidatePackageId(packageId);
        return Path.Combine(Path.GetFullPath(rootDirectory), packageId);
    }

    internal static Task SaveManifestAsync(
        string manifestPath,
        InstalledManifest manifest,
        CancellationToken cancellationToken)
        => SaveJsonAsync(manifestPath, manifest, cancellationToken);

    internal void Commit()
    {
        File.Move(this.PendingPath, this.CommittedPath);
        this.committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (this.committed)
            {
                CleanupCommitted();
            }
            else
            {
                await RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                CleanupRolledBack();
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "NuGetプラグイン操作の後処理に失敗しました: {0} {1} ({2})",
                this.state.PackageId,
                Path.GetFileName(this.operationDirectory),
                ex);
        }
    }

    internal static async Task<IReadOnlySet<string>> RecoverInterruptedOperationsAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        var operationsDirectory = Path.Combine(
            Path.GetFullPath(rootDirectory),
            OperationsDirectoryName);
        if (!Directory.Exists(operationsDirectory))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var unresolvedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operationDirectory in Directory.EnumerateDirectories(operationsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            NuGetPluginOperationState? state = null;
            try
            {
                var committedPath = Path.Combine(operationDirectory, CommittedFileName);
                var isCommitted = File.Exists(committedPath);
                var statePath = isCommitted
                    ? committedPath
                    : Path.Combine(operationDirectory, PendingFileName);
                if (!File.Exists(statePath))
                {
                    DeleteDirectoryIfExists(operationDirectory);
                    continue;
                }

                state = JsonSerializer.Deserialize<NuGetPluginOperationState>(
                    await File.ReadAllTextAsync(statePath, cancellationToken).ConfigureAwait(false),
                    NuGetPluginService.ManifestJsonOptions)
                    ?? throw new InvalidDataException("NuGetプラグイン操作情報が空です。");
                var operation = new NuGetPluginOperation(rootDirectory, operationDirectory, state)
                {
                    committed = isCommitted,
                };
                if (isCommitted)
                {
                    operation.CleanupCommitted();
                }
                else
                {
                    await operation.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    operation.CleanupRolledBack();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (!string.IsNullOrWhiteSpace(state?.PackageId))
                {
                    unresolvedPackageIds.Add(state.PackageId);
                }
                Trace.TraceWarning(
                    "NuGetプラグイン操作の復旧に失敗しました: {0} ({1})",
                    operationDirectory,
                    ex);
            }
        }

        return unresolvedPackageIds;
    }

    private async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(this.WorkingPath) && Directory.Exists(this.TargetPath))
        {
            Directory.Move(this.TargetPath, this.WorkingPath);
        }
        if (Directory.Exists(this.BackupPath))
        {
            Directory.Move(this.BackupPath, this.TargetPath);
        }

        var manifestPath = Path.Combine(this.rootDirectory, "nuget-manifest.json");
        if (this.state.OriginalManifest is { } originalManifest)
        {
            await SaveManifestAsync(
                manifestPath,
                originalManifest,
                cancellationToken).ConfigureAwait(false);
        }
        else if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
        }
    }

    private void CleanupCommitted()
        => DeleteDirectoryIfExists(this.operationDirectory);

    private void CleanupRolledBack()
    {
        // 復旧後に同じ操作を再実行しないよう、作業データより先に状態を消す。
        File.Delete(this.PendingPath);
        CleanupCommitted();
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
            var json = JsonSerializer.Serialize(value, NuGetPluginService.ManifestJsonOptions);
            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
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
