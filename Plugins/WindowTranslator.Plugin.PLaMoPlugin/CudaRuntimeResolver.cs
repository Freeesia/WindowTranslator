using System.IO.Compression;
using System.Security.Cryptography;

namespace WindowTranslator.Plugin.PLaMoPlugin;

internal static class CudaRuntimeResolver
{
    private const string CudaRelease = "12.9.0";
    private const string RedistributableBaseUrl = "https://developer.download.nvidia.com/compute/cuda/redist/";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(30) };
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);
    private static readonly string[] RequiredFiles =
        ["cudart64_12.dll", "cublasLt64_12.dll", "cublas64_12.dll"];

    // NVIDIA の redistrib_12.9.0.json に記載された Windows x64 アーカイブと SHA-256。
    private static readonly RuntimeArchive[] Archives =
    [
        new(
            "cuda_cudart/windows-x86_64/cuda_cudart-windows-x86_64-12.9.37-archive.zip",
            "f96afe6df898bc8510c48b44668bd9f825731efbf460f3640a922b2b8ae59ccc",
            ["cudart64_12.dll"]),
        new(
            "libcublas/windows-x86_64/libcublas-windows-x86_64-12.9.0.13-archive.zip",
            "20d9c2cd3810c948b875820917b38053dacf200b23cb3b8b8a14ff3569aa1f31",
            ["cublasLt64_12.dll", "cublas64_12.dll"]),
    ];

    private static string CacheDirectory => Path.Combine(
        PathUtility.UserDir, "cache", "cuda12", CudaRelease);

    internal static string? FindExistingRuntime()
        => FindExistingRuntime(
            Environment.GetEnvironmentVariable("CUDA_PATH"),
            Environment.GetEnvironmentVariable("ProgramFiles"),
            CacheDirectory);

    internal static string? FindExistingRuntime(string? cudaPath, string? programFiles, string cacheDirectory)
    {
        if (!string.IsNullOrWhiteSpace(cudaPath))
        {
            var cudaBin = Path.Combine(cudaPath, "bin");
            if (HasRequiredFiles(cudaBin))
            {
                return cudaBin;
            }
        }

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            var toolkitRoot = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA");
            if (Directory.Exists(toolkitRoot))
            {
                var versions = Directory.EnumerateDirectories(toolkitRoot, "v12.*")
                    .Select(path => (Path: path, Version: Version.TryParse(
                        Path.GetFileName(path).AsSpan(1), out var version) ? version : null))
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

        return HasRequiredFiles(cacheDirectory) ? cacheDirectory : null;
    }

    internal static Task<string> ResolveAsync(CancellationToken cancellationToken = default)
        => ResolveAsync(
            HttpClient,
            Environment.GetEnvironmentVariable("CUDA_PATH"),
            Environment.GetEnvironmentVariable("ProgramFiles"),
            CacheDirectory,
            cancellationToken);

    internal static async Task<string> ResolveAsync(
        HttpClient httpClient,
        string? cudaPath,
        string? programFiles,
        string cacheDirectory,
        CancellationToken cancellationToken = default,
        IReadOnlyList<RuntimeArchive>? archives = null)
    {
        if (!OperatingSystem.IsWindows() || !System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.Equals(
                System.Runtime.InteropServices.Architecture.X64))
        {
            throw new PlatformNotSupportedException("PLaMo CUDA は Windows x64 のみ対応しています。");
        }

        var existing = FindExistingRuntime(cudaPath, programFiles, cacheDirectory);
        if (existing is not null)
        {
            return existing;
        }

        await DownloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            existing = FindExistingRuntime(cudaPath, programFiles, cacheDirectory);
            if (existing is not null)
            {
                return existing;
            }

            var stagingDirectory = Path.Combine(
                Path.GetDirectoryName(cacheDirectory)!, $".cuda12-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);
            try
            {
                foreach (var archive in archives ?? Archives)
                {
                    var archivePath = Path.Combine(stagingDirectory, "download.zip");
                    using (var response = await httpClient.GetAsync(
                               RedistributableBaseUrl + archive.RelativePath,
                               HttpCompletionOption.ResponseHeadersRead,
                               cancellationToken).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken)
                            .ConfigureAwait(false);
                        await using var destination = File.Create(archivePath);
                        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    }

                    await using (var stream = File.OpenRead(archivePath))
                    {
                        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)
                            .ConfigureAwait(false));
                        if (!hash.Equals(archive.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException($"NVIDIA CUDA Runtime の検証に失敗しました: {archive.RelativePath}");
                        }
                    }

                    using (var zip = ZipFile.OpenRead(archivePath))
                    {
                        foreach (var fileName in archive.Files)
                        {
                            var entry = zip.Entries.SingleOrDefault(entry =>
                                entry.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase)
                                && entry.FullName.Replace('\\', '/').Contains("/bin/", StringComparison.OrdinalIgnoreCase))
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

                Directory.CreateDirectory(cacheDirectory);
                foreach (var fileName in RequiredFiles)
                {
                    File.Move(Path.Combine(stagingDirectory, fileName),
                        Path.Combine(cacheDirectory, fileName), overwrite: true);
                }
                return cacheDirectory;
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
