using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using WindowTranslator.Extensions;

namespace WindowTranslator.Plugin.PLaMoPlugin;

internal static class CudaRuntimeResolver
{
    private const string CudaRelease = "12.9.0";
    private const string RedistributableBaseUrl = "https://developer.download.nvidia.com/compute/cuda/redist/";
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);
    private static readonly string[] RequiredFiles =
        ["cudart64_12.dll", "cublasLt64_12.dll", "cublas64_12.dll"];

    // NVIDIA の redistrib_12.9.0.json に記載された Windows x64 アーカイブと SHA-256。
    private static readonly RuntimeArchive[] Archives =
    [
        new("cuda_cudart/windows-x86_64/cuda_cudart-windows-x86_64-12.9.37-archive.zip",
            "f96afe6df898bc8510c48b44668bd9f825731efbf460f3640a922b2b8ae59ccc",
            ["cudart64_12.dll"]),
        new("libcublas/windows-x86_64/libcublas-windows-x86_64-12.9.0.13-archive.zip",
            "20d9c2cd3810c948b875820917b38053dacf200b23cb3b8b8a14ff3569aa1f31",
            ["cublasLt64_12.dll", "cublas64_12.dll"]),
    ];

    private static string PLaMoDirectory => Path.Combine(
        PathUtility.UserDir, "plamo", "cuda12", CudaRelease);

    internal static string? FindExistingRuntime()
    {
        if (Environment.GetEnvironmentVariable("CUDA_PATH") is { } cudaPath)
        {
            var cudaBin = Path.Combine(cudaPath, "bin");
            if (HasRequiredFiles(cudaBin))
            {
                return cudaBin;
            }
        }

        if (Environment.GetEnvironmentVariable("ProgramFiles") is { } programFiles)
        {
            var toolkitRoot = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA");
            if (Directory.Exists(toolkitRoot))
            {
                var versions = Directory.EnumerateDirectories(toolkitRoot, "v12.*")
                    .Select(path => (Path: path, Version: Version.TryParse(Path.GetFileName(path).AsSpan(1), out var version) ? version : null))
                    .Where(candidate => candidate.Version?.Major == 12)
                    .OrderByDescending(candidate => candidate.Version);
                foreach (var candidate in versions)
                {
                    var cudaBin = Path.Combine(candidate.Path, "bin");
                    if (HasRequiredFiles(cudaBin))
                    {
                        return cudaBin;
                    }
                }
            }
        }

        return HasRequiredFiles(PLaMoDirectory) ? PLaMoDirectory : null;
    }

    internal static async Task<string> ResolveAsync(HttpClient httpClient, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || !System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.Equals(
                System.Runtime.InteropServices.Architecture.X64))
        {
            throw new PlatformNotSupportedException("PLaMo CUDA は Windows x64 のみ対応しています。");
        }

        var existing = FindExistingRuntime();
        if (existing is not null)
        {
            return existing;
        }

        await DownloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            existing = FindExistingRuntime();
            if (existing is not null)
            {
                return existing;
            }

            var stagingDirectory = Path.Combine(Path.GetDirectoryName(PLaMoDirectory)!, $".cuda12-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);
            try
            {
                foreach (var archive in Archives)
                {
                    var archivePath = Path.Combine(stagingDirectory, "download.zip");
                    logger.LogInformation("Downloading PLaMo CUDA Runtime {Archive}...", archive.RelativePath);
                    await httpClient.DownloadFile(
                        RedistributableBaseUrl + archive.RelativePath,
                        archivePath,
                        p => logger.LogInformation("Downloading PLaMo CUDA Runtime {Archive}: {Progress:P2}", archive.RelativePath, p),
                        cancellationToken).ConfigureAwait(false);

                    await using (var stream = File.OpenRead(archivePath))
                    {
                        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
                        if (!hash.Equals(archive.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException($"NVIDIA CUDA Runtime の検証に失敗しました: {archive.RelativePath}");
                        }
                    }

                    using (var zip = ZipFile.OpenRead(archivePath))
                    {
                        foreach (var fileName in archive.Files)
                        {
                            var entry = zip.Entries.SingleOrDefault(e => e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase) && e.FullName.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
                                ?? throw new InvalidDataException($"NVIDIA CUDA Runtime に {fileName} がありません。");
                            entry.ExtractToFile(Path.Combine(stagingDirectory, fileName));
                        }
                    }

                    File.Delete(archivePath);
                }

                if (!HasRequiredFiles(stagingDirectory))
                {
                    throw new InvalidDataException("NVIDIA CUDA Runtime に必要な DLL が揃っていません。");
                }

                Directory.CreateDirectory(PLaMoDirectory);
                foreach (var fileName in RequiredFiles)
                {
                    File.Move(Path.Combine(stagingDirectory, fileName), Path.Combine(PLaMoDirectory, fileName), overwrite: true);
                }
                return PLaMoDirectory;
            }
            finally
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
        finally
        {
            DownloadLock.Release();
        }
    }

    private static bool HasRequiredFiles(string directory)
        => RequiredFiles.All(file => new FileInfo(Path.Combine(directory, file)) is { Exists: true, Length: > 0 });

    internal sealed record RuntimeArchive(string RelativePath, string Sha256, string[] Files);
}
