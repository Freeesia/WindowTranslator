extern alias PLaMo;

using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using CudaRuntimeResolver = PLaMo::WindowTranslator.Plugin.PLaMoPlugin.CudaRuntimeResolver;

namespace WindowTranslator.Tests;

public sealed class CudaRuntimeResolverTests
{
    [Fact]
    public async Task ExistingCudaPathTakesPriorityOverToolkitAndCache()
    {
        var root = CreateTestDirectory();
        try
        {
            var cudaPath = Path.Combine(root, "custom");
            var toolkit = Path.Combine(root, "ProgramFiles", "NVIDIA GPU Computing Toolkit", "CUDA", "v12.9", "bin");
            var cache = Path.Combine(root, "cache");
            WriteRuntimeFiles(Path.Combine(cudaPath, "bin"));
            WriteRuntimeFiles(toolkit);
            WriteRuntimeFiles(cache);
            using var client = new HttpClient(new RejectingHandler());

            var resolved = await CudaRuntimeResolver.ResolveAsync(client, cudaPath,
                Path.Combine(root, "ProgramFiles"), cache);

            Assert.Equal(Path.Combine(cudaPath, "bin"), resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StandardToolkitSearchChoosesNewestCompleteCuda12Install()
    {
        var root = CreateTestDirectory();
        try
        {
            var programFiles = Path.Combine(root, "ProgramFiles");
            var toolkitRoot = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA");
            WriteRuntimeFiles(Path.Combine(toolkitRoot, "v12.8", "bin"));
            Directory.CreateDirectory(Path.Combine(toolkitRoot, "v12.9", "bin"));
            WriteRuntimeFiles(Path.Combine(toolkitRoot, "v12.10", "bin"));

            Assert.Equal(Path.Combine(toolkitRoot, "v12.10", "bin"),
                CudaRuntimeResolver.FindExistingRuntime(null, programFiles, Path.Combine(root, "cache")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadedRuntimeIsVerifiedExtractedAndReused()
    {
        var root = CreateTestDirectory();
        try
        {
            var cudartZip = CreateArchive(("cuda_cudart/bin/cudart64_12.dll", "cudart"));
            var cublasZip = CreateArchive(
                ("libcublas/bin/cublasLt64_12.dll", "cublasLt"),
                ("libcublas/bin/cublas64_12.dll", "cublas"));
            var archives = new CudaRuntimeResolver.RuntimeArchive[]
            {
                new("cuda_cudart.zip", Convert.ToHexString(SHA256.HashData(cudartZip)),
                    ["cudart64_12.dll"]),
                new("libcublas.zip", Convert.ToHexString(SHA256.HashData(cublasZip)),
                    ["cublasLt64_12.dll", "cublas64_12.dll"]),
            };
            using var handler = new ArchiveHandler(cudartZip, cublasZip);
            using var client = new HttpClient(handler);
            var cache = Path.Combine(root, "cache");

            var first = await CudaRuntimeResolver.ResolveAsync(client, null, null, cache,
                archives: archives);
            var second = await CudaRuntimeResolver.ResolveAsync(client, null, null, cache,
                archives: archives);

            Assert.Equal(cache, first);
            Assert.Equal(cache, second);
            Assert.Equal(2, handler.RequestCount);
            Assert.Equal("cudart", await File.ReadAllTextAsync(Path.Combine(cache, "cudart64_12.dll")));
            Assert.Equal("cublasLt", await File.ReadAllTextAsync(Path.Combine(cache, "cublasLt64_12.dll")));
            Assert.Equal("cublas", await File.ReadAllTextAsync(Path.Combine(cache, "cublas64_12.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidArchiveHashDoesNotPopulateCache()
    {
        var root = CreateTestDirectory();
        try
        {
            var archiveBytes = CreateArchive(("cuda_cudart/bin/cudart64_12.dll", "cudart"));
            using var handler = new ArchiveHandler(archiveBytes, archiveBytes);
            using var client = new HttpClient(handler);
            var cache = Path.Combine(root, "cache");
            var archives = new CudaRuntimeResolver.RuntimeArchive[]
            {
                new("cuda_cudart.zip", new string('0', 64), ["cudart64_12.dll"]),
            };

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                CudaRuntimeResolver.ResolveAsync(client, null, null, cache, archives: archives));

            Assert.False(Directory.Exists(cache));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "WindowTranslatorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteRuntimeFiles(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var fileName in new[] { "cudart64_12.dll", "cublasLt64_12.dll", "cublas64_12.dll" })
        {
            File.WriteAllText(Path.Combine(directory, fileName), "runtime");
        }
    }

    private static byte[] CreateArchive(params (string Path, string Content)[] files)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open());
                writer.Write(content);
            }
        }
        return output.ToArray();
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("A download was not expected.");
    }

    private sealed class ArchiveHandler(byte[] cudartZip, byte[] cublasZip) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.RequestCount++;
            var bytes = request.RequestUri?.AbsolutePath.EndsWith("cuda_cudart.zip", StringComparison.Ordinal) == true
                ? cudartZip
                : cublasZip;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes),
            });
        }
    }
}
