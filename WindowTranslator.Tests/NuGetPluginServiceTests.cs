using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Resources;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Versioning;
using Weikio.PluginFramework.Catalogs;
using Weikio.PluginFramework.Context;
using WindowTranslator.Modules;
using WindowTranslator.Modules.PluginStore;

namespace WindowTranslator.Tests;

public sealed class NuGetPluginServiceTests
{
    private static readonly string RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier;

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
                        [$"runtimes/{RuntimeIdentifier}/native/root-native.dll"] = "native"u8.ToArray(),
                        [$"lib/net10.0/runtimes/{RuntimeIdentifier}/native/custom-native.dll"] = "custom"u8.ToArray(),
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
                    RuntimeIdentifier,
                    "native",
                    "root-native.dll")));
            Assert.Equal(
                "custom",
                await File.ReadAllTextAsync(Path.Combine(
                    pluginDirectory,
                    "runtimes",
                    RuntimeIdentifier,
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
    public async Task UninstallRemovesManagedFilesImmediatelyAndAllowsManualReinstall()
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

            Assert.False(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            Assert.Empty(await service.GetInstalledPackagesAsync());

            await service.InstallPackageAsync("Root.Plugin", "2.0.0");
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
    public async Task UninstallRestoresManagedFilesWhenManifestUpdateFails()
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

            using var client = new HttpClient(handler);
            using var service = CreateService(client, testDirectory);
            await service.InstallPackageAsync("Root.Plugin", "1.0.0");

            var manifestPath = Path.Combine(testDirectory, "nuget-manifest.json");
            using (var manifestLock = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                var exception = await Record.ExceptionAsync(
                    () => service.UninstallPackageAsync("Root.Plugin"));
                Assert.True(
                    exception is IOException or UnauthorizedAccessException,
                    $"Unexpected exception: {exception}");
            }

            Assert.True(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            Assert.Empty(Directory.GetDirectories(
                testDirectory,
                "Root.Plugin.uninstalling-*"));
            var installed = Assert.Single(await service.GetInstalledPackagesAsync());
            Assert.Equal("1.0.0", installed.Version);
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
                        [$"runtimes/{RuntimeIdentifier}/native/compatible.dll"] = "native"u8.ToArray(),
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
                    RuntimeIdentifier,
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
    public async Task ExistingManifestLoadsInstalledPackages()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "nuget-manifest.json"),
                JsonSerializer.Serialize(new
                {
                    Packages = new[]
                    {
                        new
                        {
                            Id = "Legacy.Plugin",
                            Version = "1.0.0",
                        },
                    },
                }));

            using var handler = new InMemoryNuGetHandler();
            using var client = new HttpClient(handler);
            using var service = CreateService(client, testDirectory);

            var installed = Assert.Single(await service.GetInstalledPackagesAsync());

            Assert.Equal("Legacy.Plugin", installed.Id);
            Assert.Equal("1.0.0", installed.Version);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task PluginStoreKeepsInstalledPackagesVisibleWhenNuGetSearchFails()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "nuget-manifest.json"),
                JsonSerializer.Serialize(new InstalledManifest(
                    [new InstalledPackageInfo("Installed.Plugin", "1.2.3")])),
                Encoding.UTF8);
            using var handler = new InMemoryNuGetHandler();
            using var client = new HttpClient(handler);
            using var service = CreateService(client, testDirectory);
            var viewModel = new PluginStoreViewModel(
                service,
                NullLogger<PluginStoreViewModel>.Instance,
                dialogService: null!);

            await viewModel.LoadAsync();

            var package = Assert.Single(viewModel.Packages);
            Assert.Equal("Installed.Plugin", package.Id);
            Assert.Equal("1.2.3", package.InstalledVersion);
            Assert.True(package.IsInstalled);
            Assert.NotNull(viewModel.ErrorMessage);
            Assert.Contains(
                handler.RequestedPaths,
                path => path.Equals("/v3/index.json", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task InstallRejectsPackageRequiringNewerHostAbstractions()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.AddPackage(
                "Root.Plugin",
                "2.0.0",
                CreatePackage(
                    "Root.Plugin",
                    "2.0.0",
                    [
                        new(
                            "WindowTranslator.Abstractions",
                            "[2.0.0, 3.0.0)",
                            Exclude: "Runtime"),
                    ],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "root"u8.ToArray(),
                    }));

            using var client = new HttpClient(handler);
            using var service = CreateService(
                client,
                testDirectory,
                new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WindowTranslator.Abstractions"] = NuGetVersion.Parse("1.5.0"),
                });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.InstallPackageAsync("Root.Plugin", "2.0.0"));

            Assert.Contains("WindowTranslator.Abstractions", exception.Message);
            Assert.Contains("[2.0.0, 3.0.0)", exception.Message);
            Assert.False(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            Assert.Empty(await service.GetInstalledPackagesAsync());
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task InstallAcceptsCompatibleHostAbstractionsWithoutDownloadingIt()
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
                        new(
                            "WindowTranslator.Abstractions",
                            "[1.0.0, 2.0.0)"),
                    ],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Root.Plugin.dll"] = "root"u8.ToArray(),
                    }));

            using var client = new HttpClient(handler);
            using var service = CreateService(
                client,
                testDirectory,
                new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WindowTranslator.Abstractions"] = NuGetVersion.Parse("1.5.0"),
                });

            await service.InstallPackageAsync("Root.Plugin", "1.0.0");

            Assert.True(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            Assert.DoesNotContain(
                handler.RequestedPaths,
                path => path.Contains(
                    "windowtranslator.abstractions",
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void CatalogSynchronizationCopiesOnlyChangesAndRemovesStaleFiles()
    {
        var sourceDirectory = CreateTestDirectory();
        var destinationDirectory = CreateTestDirectory();
        try
        {
            File.WriteAllText(Path.Combine(sourceDirectory, "Legacy.Plugin.dll"), "legacy");
            File.WriteAllText(Path.Combine(sourceDirectory, "nuget-manifest.json"), "{}");
            File.WriteAllText(Path.Combine(sourceDirectory, "nuget-manifest.json.tmp-test"), "{}");
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Root.Plugin"));
            var sourcePluginPath =
                Path.Combine(sourceDirectory, "Root.Plugin", "Root.Plugin.dll");
            File.WriteAllText(sourcePluginPath, "plugin-new");
            var unchangedSourcePath =
                Path.Combine(sourceDirectory, "Root.Plugin", "Unchanged.dll");
            File.WriteAllText(unchangedSourcePath, "unchanged");
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Empty.Plugin"));
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Root.Plugin.backup-test"));
            File.WriteAllText(
                Path.Combine(sourceDirectory, "Root.Plugin.backup-test", "old.dll"),
                "old");
            Directory.CreateDirectory(Path.Combine(sourceDirectory, ".Root.Plugin.installing-test"));
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Root.Plugin.uninstalling-test"));
            Directory.CreateDirectory(Path.Combine(destinationDirectory, "Root.Plugin"));
            var destinationPluginPath =
                Path.Combine(destinationDirectory, "Root.Plugin", "Root.Plugin.dll");
            File.WriteAllText(destinationPluginPath, "plugin-old");
            var unchangedDestinationPath =
                Path.Combine(destinationDirectory, "Root.Plugin", "Unchanged.dll");
            File.WriteAllText(unchangedDestinationPath, "unchanged");
            var unchangedTimestamp = DateTime.UtcNow.AddMinutes(-5);
            File.SetLastWriteTimeUtc(unchangedSourcePath, unchangedTimestamp);
            File.SetLastWriteTimeUtc(unchangedDestinationPath, unchangedTimestamp);
            File.SetCreationTimeUtc(unchangedSourcePath, unchangedTimestamp);
            File.SetCreationTimeUtc(unchangedDestinationPath, unchangedTimestamp);
            File.SetLastWriteTimeUtc(sourcePluginPath, unchangedTimestamp);
            File.SetLastWriteTimeUtc(destinationPluginPath, unchangedTimestamp);
            File.SetCreationTimeUtc(
                sourcePluginPath,
                unchangedTimestamp.AddMinutes(2));
            File.SetCreationTimeUtc(
                destinationPluginPath,
                unchangedTimestamp.AddMinutes(-2));
            File.WriteAllText(
                Path.Combine(destinationDirectory, "Root.Plugin", "Removed.dll"),
                "stale");
            Directory.CreateDirectory(Path.Combine(destinationDirectory, "Removed.Plugin"));
            File.WriteAllText(
                Path.Combine(destinationDirectory, "Removed.Plugin", "Removed.Plugin.dll"),
                "stale");
            File.WriteAllText(
                Path.Combine(destinationDirectory, "nuget-manifest.json"),
                "{}");

            using var unchangedFileLock = new FileStream(
                unchangedDestinationPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            NuGetPluginCatalog.SynchronizePluginFiles(
                sourceDirectory,
                destinationDirectory);
            using var synchronizedFileLock = new FileStream(
                destinationPluginPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            NuGetPluginCatalog.SynchronizePluginFiles(
                sourceDirectory,
                destinationDirectory);

            Assert.True(File.Exists(Path.Combine(destinationDirectory, "Legacy.Plugin.dll")));
            Assert.Equal(
                "plugin-new",
                File.ReadAllText(destinationPluginPath));
            Assert.Equal("unchanged", File.ReadAllText(unchangedDestinationPath));
            Assert.False(File.Exists(
                Path.Combine(destinationDirectory, "Root.Plugin", "Removed.dll")));
            Assert.False(Directory.Exists(
                Path.Combine(destinationDirectory, "Removed.Plugin")));
            Assert.True(Directory.Exists(
                Path.Combine(destinationDirectory, "Empty.Plugin")));
            Assert.False(File.Exists(Path.Combine(destinationDirectory, "nuget-manifest.json")));
            Assert.False(File.Exists(
                Path.Combine(destinationDirectory, "nuget-manifest.json.tmp-test")));
            Assert.False(Directory.Exists(
                Path.Combine(destinationDirectory, "Root.Plugin.backup-test")));
            Assert.False(Directory.Exists(
                Path.Combine(destinationDirectory, ".Root.Plugin.installing-test")));
            Assert.False(Directory.Exists(
                Path.Combine(destinationDirectory, "Root.Plugin.uninstalling-test")));
        }
        finally
        {
            DeleteTestDirectory(sourceDirectory);
            DeleteTestDirectory(destinationDirectory);
        }
    }

    [Fact]
    public void CatalogSynchronizationClearsStaleFilesWhenSourceIsMissing()
    {
        var sourceDirectory = CreateTestDirectory();
        var destinationDirectory = CreateTestDirectory();
        try
        {
            Directory.Delete(sourceDirectory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, "Removed.Plugin"));
            File.WriteAllText(
                Path.Combine(destinationDirectory, "Removed.Plugin", "Removed.Plugin.dll"),
                "stale");

            NuGetPluginCatalog.SynchronizePluginFiles(
                sourceDirectory,
                destinationDirectory);

            Assert.Empty(Directory.EnumerateFileSystemEntries(destinationDirectory));
        }
        finally
        {
            DeleteTestDirectory(sourceDirectory);
            DeleteTestDirectory(destinationDirectory);
        }
    }

    [Fact]
    public async Task CatalogLoadsARealAssemblyFromAPackageSubdirectory()
    {
        var sourceDirectory = CreateTestDirectory();
        var tempDirectory = CreateTestDirectory();
        try
        {
            var packageDirectory = Path.Combine(sourceDirectory, "Catalog.Probe");
            Directory.CreateDirectory(packageDirectory);
            var testAssemblyPath = typeof(NuGetPluginServiceTests).Assembly.Location;
            File.Copy(
                testAssemblyPath,
                Path.Combine(packageDirectory, Path.GetFileName(testAssemblyPath)));
            var runtimeDirectory = Path.Combine(
                packageDirectory,
                "runtimes",
                "win",
                "lib",
                "net10.0");
            Directory.CreateDirectory(runtimeDirectory);
            File.Copy(
                typeof(NuGetVersion).Assembly.Location,
                Path.Combine(runtimeDirectory, "NuGet.Versioning.dll"));
            Assert.Empty(Directory.EnumerateFiles(
                packageDirectory,
                "*.deps.json",
                SearchOption.AllDirectories));

            var options = new FolderPluginCatalogOptions();
            options.TypeFinderOptions.TypeFinderCriterias.Clear();
            options.TypeFinderOptions.TypeFinderCriterias.Add(new()
            {
                Query = static (_, type) =>
                    type.Name == nameof(CatalogProbeTranslateModule),
            });
            options.PluginLoadContextOptions.UseHostApplicationAssemblies =
                UseHostApplicationAssembliesEnum.Selected;
            options.PluginLoadContextOptions.HostApplicationAssemblies =
                AssemblyLoadContext.Default.Assemblies
                    .Where(assembly => !assembly.IsDynamic
                        && assembly != typeof(NuGetPluginServiceTests).Assembly
                        && !string.Equals(
                            assembly.GetName().Name,
                            typeof(NuGetVersion).Assembly.GetName().Name,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(assembly => assembly.GetName())
                    .ToList();
            options.PluginLoadContextOptions.AdditionalRuntimePaths = [];
            var catalog = new NuGetPluginCatalog(
                sourceDirectory,
                tempDirectory,
                options);

            await catalog.Initialize();

            Assert.True(catalog.IsInitialized);
            var plugin = Assert.Single(
                catalog.GetPlugins(),
                plugin => plugin.Type.Name == nameof(CatalogProbeTranslateModule));
            var module = Assert.IsAssignableFrom<ITranslateModule>(
                Activator.CreateInstance(plugin.Type));
            var translated = await module.TranslateAsync([new("source", null)]);
            Assert.Equal("1.2.3", Assert.Single(translated));
        }
        finally
        {
            DeleteTestDirectory(sourceDirectory);
            DeleteTestDirectory(tempDirectory);
        }
    }

    [Fact]
    public async Task CatalogLoadsTheSatelliteAssemblyForTheRequestedCulture()
    {
        var sourceDirectory = CreateTestDirectory();
        var tempDirectory = CreateTestDirectory();
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var packageDirectory = Path.Combine(sourceDirectory, "Catalog.Probe");
            Directory.CreateDirectory(packageDirectory);
            var testAssemblyPath = typeof(NuGetPluginServiceTests).Assembly.Location;
            File.Copy(
                testAssemblyPath,
                Path.Combine(packageDirectory, Path.GetFileName(testAssemblyPath)));
            foreach (var cultureName in new[] { "ar", "fr" })
            {
                var cultureDirectory = Path.Combine(packageDirectory, cultureName);
                Directory.CreateDirectory(cultureDirectory);
                File.Copy(
                    Path.Combine(
                        Path.GetDirectoryName(testAssemblyPath)!,
                        cultureName,
                        "WindowTranslator.Tests.resources.dll"),
                    Path.Combine(cultureDirectory, "WindowTranslator.Tests.resources.dll"));
            }

            var options = new FolderPluginCatalogOptions();
            options.TypeFinderOptions.TypeFinderCriterias.Clear();
            options.TypeFinderOptions.TypeFinderCriterias.Add(new()
            {
                Query = static (_, type) =>
                    type.Name == nameof(CatalogProbeLocalizedTranslateModule),
            });
            options.PluginNameOptions.PluginNameGenerator = static (_, type) =>
                new ResourceManager(
                    "WindowTranslator.Tests.CatalogProbeResources",
                    type.Assembly).GetString("Greeting", CultureInfo.CurrentUICulture)
                ?? type.Name;
            options.PluginLoadContextOptions.UseHostApplicationAssemblies =
                UseHostApplicationAssembliesEnum.Selected;
            options.PluginLoadContextOptions.HostApplicationAssemblies =
                AssemblyLoadContext.Default.Assemblies
                    .Where(assembly => !assembly.IsDynamic
                        && assembly != typeof(NuGetPluginServiceTests).Assembly)
                    .Select(assembly => assembly.GetName())
                    .ToList();
            options.PluginLoadContextOptions.AdditionalRuntimePaths = [];
            var catalog = new NuGetPluginCatalog(
                sourceDirectory,
                tempDirectory,
                options);

            await catalog.Initialize();

            var plugin = Assert.Single(
                catalog.GetPlugins(),
                plugin => plugin.Type.Name == nameof(CatalogProbeLocalizedTranslateModule));
            Assert.Equal("français", plugin.Name);
            Assert.NotSame(
                AssemblyLoadContext.Default,
                AssemblyLoadContext.GetLoadContext(plugin.Type.Assembly));
            var module = Assert.IsAssignableFrom<ITranslateModule>(
                Activator.CreateInstance(plugin.Type));
            var translated = await module.TranslateAsync([new("source", null)]);
            Assert.Equal("français", Assert.Single(translated));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
            DeleteTestDirectory(sourceDirectory);
            DeleteTestDirectory(tempDirectory);
        }
    }

    [Fact]
    public void FrameworkSelectionPrefersTheCompatibleWindowsTarget()
    {
        Assert.Equal(
            "net10.0-windows10.0.20348.0",
            NuGetPackageInstaller.SelectBestTfm(
                ["net10.0", "net10.0-windows10.0.20348.0", "netstandard2.0"]));
        Assert.Equal(
            "net10.0-windows10.0.19041.0",
            NuGetPackageInstaller.SelectBestTfm(
                [
                    "net10.0",
                    "net10.0-windows10.0.19041.0",
                    "net10.0-windows10.0.22621.0",
                ]));
        Assert.Equal(
            ".NETCoreApp,Version=v10.0",
            NuGetPackageInstaller.SelectBestTfm([".NETCoreApp,Version=v10.0"]));
        Assert.Null(NuGetPackageInstaller.SelectBestTfm(
            ["net10.0-windows10.0.22621.0"]));
        Assert.Null(NuGetPackageInstaller.SelectBestTfm(["net11.0-windows"]));
        Assert.Null(NuGetPackageInstaller.SelectBestTfm(["net48"]));
    }

    private static NuGetPluginService CreateService(
        HttpClient client,
        string pluginDirectory,
        IReadOnlyDictionary<string, NuGetVersion>? hostPackageVersions = null)
        => new(
            NullLogger<NuGetPluginService>.Instance,
            client,
            pluginDirectory,
            hostPackageVersions: hostPackageVersions);

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

public sealed class CatalogProbeTranslateModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => ValueTask.FromResult(
            Enumerable.Repeat(
                new NuGetVersion(1, 2, 3).ToNormalizedString(),
                srcTexts.Length).ToArray());
}

public sealed class CatalogProbeLocalizedTranslateModule : ITranslateModule
{
    private static readonly ResourceManager Resources = new(
        "WindowTranslator.Tests.CatalogProbeResources",
        typeof(CatalogProbeLocalizedTranslateModule).Assembly);

    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => ValueTask.FromResult(
            Enumerable.Repeat(
                Resources.GetString("Greeting", CultureInfo.CurrentUICulture) ?? string.Empty,
                srcTexts.Length).ToArray());
}
