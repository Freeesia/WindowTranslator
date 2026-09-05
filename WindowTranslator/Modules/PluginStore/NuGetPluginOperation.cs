using System.Diagnostics;
using System.IO;
using NuGet.Packaging;

namespace WindowTranslator.Modules.PluginStore;

internal sealed class NuGetPluginOperation : IDisposable
{
    private bool committed;

    internal NuGetPluginOperation(string rootDirectory, string packageId)
    {
        PackageIdValidator.ValidatePackageId(packageId);
        var rootPath = Path.GetFullPath(rootDirectory);
        var operationId = Guid.NewGuid().ToString("N");
        this.TargetPath = Path.Combine(rootPath, packageId);
        this.WorkingPath = Path.Combine(rootPath, $"{packageId}.installing-{operationId}");
        this.BackupPath = Path.Combine(rootPath, $"{packageId}.backup-{operationId}");
        Directory.CreateDirectory(this.WorkingPath);
    }

    internal string TargetPath { get; }

    internal string WorkingPath { get; }

    internal string BackupPath { get; }

    internal void Commit() => this.committed = true;

    public void Dispose()
    {
        try
        {
            if (!this.committed)
            {
                if (!Directory.Exists(this.WorkingPath) && Directory.Exists(this.TargetPath))
                {
                    Directory.Move(this.TargetPath, this.WorkingPath);
                }
                if (Directory.Exists(this.BackupPath))
                {
                    Directory.Move(this.BackupPath, this.TargetPath);
                }
            }

            DeleteDirectoryIfExists(this.WorkingPath);
            DeleteDirectoryIfExists(this.BackupPath);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("NuGet plugin operation cleanup failed: {0} ({1})", this.TargetPath, ex);
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
