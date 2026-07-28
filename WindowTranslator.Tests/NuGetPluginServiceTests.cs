using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using WindowTranslator.Modules.PluginStore;

namespace WindowTranslator.Tests;

public sealed class NuGetPluginServiceTests
{
    [Fact]
    public async Task InstallResolvesRuntimeDependenciesAndPreservesAssetDirectories()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.AddPackage(
                "Root.Plugin",
                "1.0.0",
                CreatePackage(
                    "Root.Plugin",
                    "1.0.0",
                    [
                        new("Dependency.Package", "[1.0.0, 2.0.0)"),
                        new("Host.Provided", "[1.0.0]", Exclude: "Runtime"),
                    ],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "root"u8.ToArray(),
                        ["lib/net10.0/fr/Root.Plugin.resources.dll"] = "fr"u8.ToArray(),
                        ["runtimes/win-x64/native/root-native.dll"] = "native"u8.ToArray(),
                        ["lib/net10.0/runtimes/win-x64/native/custom-native.dll"] = "custom"u8.ToArray(),
                    }));
            handler.AddPackage(
                "Dependency.Package",
                "1.0.0",
                CreatePackage(
                    "Dependency.Package",
                    "1.0.0",
                    [new("Transitive.Package", "[2.0.0]")],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net8.0/Dependency.Package.dll"] = "dependency"u8.ToArray(),
                    }));
            handler.AddPackage(
                "Transitive.Package",
                "2.0.0",
                CreatePackage(
                    "Transitive.Package",
                    "2.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/netstandard2.0/Transitive.Package.dll"] = "transitive"u8.ToArray(),
                    }));

            using var client = new HttpClient(handler);
            using var service = CreateService(client, testDirectory);

            await service.InstallPackageAsync("Root.Plugin", "1.0.0");

            var pluginDirectory = Path.Combine(testDirectory, "Root.Plugin");
            Assert.Equal("root", await File.ReadAllTextAsync(Path.Combine(pluginDirectory, "Root.Plugin.dll")));
            Assert.Equal(
                "fr",
                await File.ReadAllTextAsync(Path.Combine(pluginDirectory, "fr", "Root.Plugin.resources.dll")));
            Assert.Equal(
                "dependency",
                await File.ReadAllTextAsync(Path.Combine(pluginDirectory, "Dependency.Package.dll")));
            Assert.Equal(
                "transitive",
                await File.ReadAllTextAsync(Path.Combine(pluginDirectory, "Transitive.Package.dll")));
            Assert.Equal(
                "native",
                await File.ReadAllTextAsync(Path.Combine(
                    pluginDirectory,
                    "runtimes",
                    "win-x64",
                    "native",
                    "root-native.dll")));
            Assert.Equal(
                "custom",
                await File.ReadAllTextAsync(Path.Combine(
                    pluginDirectory,
                    "runtimes",
                    "win-x64",
                    "native",
                    "custom-native.dll")));
            Assert.DoesNotContain(
                handler.RequestedPaths,
                path => path.Contains("host.provided", StringComparison.OrdinalIgnoreCase));

            var installed = await service.GetInstalledPackagesAsync();
            var package = Assert.Single(installed);
            Assert.Equal("Root.Plugin", package.Id);
            Assert.Equal("1.0.0", package.Version);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task ManifestWriteFailureRestoresThePreviousPluginDirectory()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.AddPackage(
                "Root.Plugin",
                "1.0.0",
                CreatePackage(
                    "Root.Plugin",
                    "1.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "version-one"u8.ToArray(),
                    }));
            handler.AddPackage(
                "Root.Plugin",
                "2.0.0",
                CreatePackage(
                    "Root.Plugin",
                    "2.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "version-two"u8.ToArray(),
                    }));

            using var client = new HttpClient(handler);
            using var service = CreateService(client, testDirectory);
            await service.InstallPackageAsync("Root.Plugin", "1.0.0");

            var manifestPath = Path.Combine(testDirectory, "nuget-manifest.json");
            await using (var manifestLock = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                var exception = await Record.ExceptionAsync(
                    () => service.InstallPackageAsync("Root.Plugin", "2.0.0"));
                Assert.True(
                    exception is IOException or UnauthorizedAccessException,
                    $"Unexpected exception: {exception}");
            }

            Assert.Equal(
                "version-one",
                await File.ReadAllTextAsync(
                    Path.Combine(testDirectory, "Root.Plugin", "Root.Plugin.dll")));
            var installed = Assert.Single(await service.GetInstalledPackagesAsync());
            Assert.Equal("1.0.0", installed.Version);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task ReinstallAfterUninstallRemovesThePendingDeletionMarker()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.AddPackage(
                "Root.Plugin",
                "1.0.0",
                CreatePackage(
                    "Root.Plugin",
                    "1.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "version-one"u8.ToArray(),
                    }));
            handler.AddPackage(
                "Root.Plugin",
                "2.0.0",
                CreatePackage(
                    "Root.Plugin",
                    "2.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "version-two"u8.ToArray(),
                    }));

            using var client = new HttpClient(handler);
            using var service = CreateService(client, testDirectory);
            await service.InstallPackageAsync("Root.Plugin", "1.0.0");
            await service.UninstallPackageAsync("Root.Plugin");

            var markerPath = Path.Combine(testDirectory, "Root.Plugin.pending-delete");
            Assert.True(File.Exists(markerPath));

            await service.InstallPackageAsync("Root.Plugin", "2.0.0");
            Assert.False(File.Exists(markerPath));

            service.ProcessPendingDeletions();
            Assert.True(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            var installed = Assert.Single(await service.GetInstalledPackagesAsync());
            Assert.Equal("2.0.0", installed.Version);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task DependencyWithIncompatibleLibStillInstallsCompatibleNativeAssets()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.AddPackage(
                "Root.Plugin",
                "1.0.0",
                CreatePackage(
                    "Root.Plugin",
                    "1.0.0",
                    [new("Native.Dependency", "[1.0.0]")],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "root"u8.ToArray(),
                    }));
            handler.AddPackage(
                "Native.Dependency",
                "1.0.0",
                CreatePackage(
                    "Native.Dependency",
                    "1.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net48/LegacyOnly.dll"] = "legacy"u8.ToArray(),
                        ["runtimes/win-x64/native/compatible.dll"] = "native"u8.ToArray(),
                    }));

            using var client = new HttpClient(handler);
            using var service = CreateService(client, testDirectory);

            await service.InstallPackageAsync("Root.Plugin", "1.0.0");

            Assert.False(File.Exists(
                Path.Combine(testDirectory, "Root.Plugin", "LegacyOnly.dll")));
            Assert.Equal(
                "native",
                await File.ReadAllTextAsync(Path.Combine(
                    testDirectory,
                    "Root.Plugin",
                    "runtimes",
                    "win-x64",
                    "native",
                    "compatible.dll")));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task InvalidManifestDoesNotReplaceExistingPluginDirectory()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var pluginDirectory = Path.Combine(testDirectory, "Root.Plugin");
            Directory.CreateDirectory(pluginDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(pluginDirectory, "Root.Plugin.dll"),
                "existing");
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "nuget-manifest.json"),
                "{ invalid");

            using var handler = new InMemoryNuGetHandler();
            handler.AddPackage(
                "Root.Plugin",
                "2.0.0",
                CreatePackage(
                    "Root.Plugin",
                    "2.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "replacement"u8.ToArray(),
                    }));

            using var client = new HttpClient(handler);
            using var service = CreateService(client, testDirectory);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.InstallPackageAsync("Root.Plugin", "2.0.0"));

            Assert.Equal(
                "existing",
                await File.ReadAllTextAsync(
                    Path.Combine(pluginDirectory, "Root.Plugin.dll")));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void CatalogCopyIncludesLegacyRootFilesAndSkipsManagementState()
    {
        var sourceDirectory = CreateTestDirectory();
        var destinationDirectory = CreateTestDirectory();
        try
        {
            File.WriteAllText(Path.Combine(sourceDirectory, "Legacy.Plugin.dll"), "legacy");
            File.WriteAllText(Path.Combine(sourceDirectory, "nuget-manifest.json"), "{}");
            File.WriteAllText(Path.Combine(sourceDirectory, "nuget-manifest.json.tmp-test"), "{}");
            File.WriteAllText(Path.Combine(sourceDirectory, "Root.Plugin.pending-delete"), "Root.Plugin");
            File.WriteAllText(
                Path.Combine(sourceDirectory, "Root.Plugin.pending-delete.tmp-test"),
                "Root.Plugin");
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Root.Plugin"));
            File.WriteAllText(
                Path.Combine(sourceDirectory, "Root.Plugin", "Root.Plugin.dll"),
                "plugin");
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Root.Plugin.backup-test"));
            File.WriteAllText(
                Path.Combine(sourceDirectory, "Root.Plugin.backup-test", "old.dll"),
                "old");
            Directory.CreateDirectory(Path.Combine(sourceDirectory, ".Root.Plugin.installing-test"));
            Directory.CreateDirectory(Path.Combine(destinationDirectory, "Root.Plugin"));
            File.WriteAllText(
                Path.Combine(destinationDirectory, "Root.Plugin", "Root.Plugin.dll"),
                "existing");

            NuGetPluginCatalog.CopyPluginFiles(sourceDirectory, destinationDirectory);

            Assert.True(File.Exists(Path.Combine(destinationDirectory, "Legacy.Plugin.dll")));
            Assert.Equal(
                "existing",
                File.ReadAllText(
                    Path.Combine(destinationDirectory, "Root.Plugin", "Root.Plugin.dll")));
            Assert.False(File.Exists(Path.Combine(destinationDirectory, "nuget-manifest.json")));
            Assert.False(File.Exists(
                Path.Combine(destinationDirectory, "nuget-manifest.json.tmp-test")));
            Assert.False(File.Exists(Path.Combine(destinationDirectory, "Root.Plugin.pending-delete")));
            Assert.False(File.Exists(
                Path.Combine(destinationDirectory, "Root.Plugin.pending-delete.tmp-test")));
            Assert.False(Directory.Exists(
                Path.Combine(destinationDirectory, "Root.Plugin.backup-test")));
            Assert.False(Directory.Exists(
                Path.Combine(destinationDirectory, ".Root.Plugin.installing-test")));
        }
        finally
        {
            DeleteTestDirectory(sourceDirectory);
            DeleteTestDirectory(destinationDirectory);
        }
    }

    [Fact]
    public void FrameworkSelectionPrefersTheCompatibleWindowsTarget()
    {
        Assert.Equal(
            "net10.0-windows10.0.20348.0",
            NuGetPackageInstaller.SelectBestTfm(
                ["net10.0", "net10.0-windows10.0.20348.0", "netstandard2.0"]));
        Assert.Null(NuGetPackageInstaller.SelectBestTfm(["net48"]));
    }

    private static NuGetPluginService CreateService(HttpClient client, string pluginDirectory)
        => new(
            NullLogger<NuGetPluginService>.Instance,
            client,
            pluginDirectory);

    private static byte[] CreatePackage(
        string id,
        string version,
        IReadOnlyCollection<TestDependency> dependencies,
        IReadOnlyDictionary<string, byte[]> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var dependencyElements = dependencies.Select(dependency =>
            {
                var element = new XElement(
                    "dependency",
                    new XAttribute("id", dependency.Id),
                    new XAttribute("version", dependency.Version));
                if (dependency.Exclude is not null)
                {
                    element.Add(new XAttribute("exclude", dependency.Exclude));
                }
                return element;
            });
            var nuspec = new XDocument(
                new XElement(
                    "package",
                    new XElement(
                        "metadata",
                        new XElement("id", id),
                        new XElement("version", version),
                        new XElement("authors", "WindowTranslator.Tests"),
                        new XElement("description", "Test package"),
                        new XElement(
                            "dependencies",
                            new XElement(
                                "group",
                                new XAttribute("targetFramework", "net10.0"),
                                dependencyElements)))));
            var nuspecEntry = archive.CreateEntry($"{id}.nuspec");
            using (var nuspecStream = nuspecEntry.Open())
            {
                nuspec.Save(nuspecStream);
            }

            foreach (var (path, content) in entries)
            {
                var entry = archive.CreateEntry(path);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "WindowTranslator.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTestDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // テスト用ディレクトリの後始末はテスト結果へ影響させない
        }
    }

    private sealed record TestDependency(string Id, string Version, string? Exclude = null);

    private sealed class InMemoryNuGetHandler : HttpMessageHandler
    {
        private readonly Dictionary<(string Id, string Version), byte[]> packages = new();

        public List<string> RequestedPaths { get; } = [];

        public void AddPackage(string id, string version, byte[] package)
            => this.packages[(id.ToLowerInvariant(), version.ToLowerInvariant())] = package;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            this.RequestedPaths.Add(path);
            var segments = path.Trim('/').Split('/');
            if (segments.Length == 3
                && segments[0].Equals("v3-flatcontainer", StringComparison.OrdinalIgnoreCase)
                && segments[2].Equals("index.json", StringComparison.OrdinalIgnoreCase))
            {
                var id = segments[1].ToLowerInvariant();
                var versions = this.packages.Keys
                    .Where(key => key.Id == id)
                    .Select(key => key.Version)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(version => version, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { versions }),
                        Encoding.UTF8,
                        "application/json"),
                });
            }

            if (segments.Length == 4
                && segments[0].Equals("v3-flatcontainer", StringComparison.OrdinalIgnoreCase))
            {
                var key = (segments[1].ToLowerInvariant(), segments[2].ToLowerInvariant());
                if (this.packages.TryGetValue(key, out var package))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(package),
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
