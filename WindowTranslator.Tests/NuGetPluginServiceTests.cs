using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Reflection.Emit;
using System.Resources;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Weikio.PluginFramework.Abstractions;
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

            using var service = CreateService(handler, testDirectory, hostMajorVersion: 7);
            var progress = new RecordingProgress();

            await service.InstallPackageAsync("Root.Plugin", "1.0.0", progress);

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

            var installed = service.PackageSnapshot.InstalledPackages;
            var package = Assert.Single(installed);
            Assert.Equal("Root.Plugin", package.Id);
            Assert.Equal("1.0.0", package.Version);
            Assert.Equal(7, package.HostMajorVersion);
            Assert.True(package.IsCompatible);
            Assert.Equal(0, progress.Values.First());
            Assert.Equal(100, progress.Values.Last());
            Assert.All(progress.Values, value => Assert.InRange(value, 0, 100));
            Assert.True(progress.Values.SequenceEqual(progress.Values.OrderBy(value => value)));
            Assert.Contains(progress.Values, value => value is > 0 and < 100);
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

            using var service = CreateService(handler, testDirectory);
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
            var installed = Assert.Single(service.PackageSnapshot.InstalledPackages);
            Assert.Equal("1.0.0", installed.Version);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task UninstallDeletesManagedFilesAtNextStartupAndAllowsManualReinstall()
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

            using var service = CreateService(handler, testDirectory);
            await service.InstallPackageAsync("Root.Plugin", "1.0.0");
            await service.UninstallPackageAsync("Root.Plugin");

            Assert.True(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            Assert.Empty(service.PackageSnapshot.InstalledPackages);

            NuGetPluginCatalog.DeleteUninstalledPackageDirectories(testDirectory);
            Assert.False(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));

            await service.InstallPackageAsync("Root.Plugin", "2.0.0");
            Assert.True(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            var installed = Assert.Single(service.PackageSnapshot.InstalledPackages);
            Assert.Equal("2.0.0", installed.Version);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task UninstallKeepsManagedFilesWhenManifestUpdateFails()
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

            using var service = CreateService(handler, testDirectory);
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
            var installed = Assert.Single(service.PackageSnapshot.InstalledPackages);
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

            using var service = CreateService(handler, testDirectory);

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

            using var service = CreateService(handler, testDirectory);

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
    public async Task ManifestLoadsInstalledPackages()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "nuget-manifest.json"),
                JsonSerializer.Serialize(new InstalledManifest(
                    [new InstalledPackageInfo(
                        "Root.Plugin",
                        "1.0.0",
                        HostMajorVersion: 7,
                        AbstractionsVersionRange: "[1.0.0, 2.0.0)")])));

            using var handler = new InMemoryNuGetHandler();
            using var service = CreateService(
                handler,
                testDirectory,
                hostMajorVersion: 7);

            await service.RefreshPackageInformationAsync();
            var installed = Assert.Single(service.PackageSnapshot.InstalledPackages);

            Assert.Equal("Root.Plugin", installed.Id);
            Assert.Equal("1.0.0", installed.Version);
            Assert.Equal(7, installed.HostMajorVersion);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task ManifestWithoutHostMajorVersionIsRejected()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(testDirectory, "Legacy.Plugin"));
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
            using var service = CreateService(
                handler,
                testDirectory,
                hostMajorVersion: 7);

            await service.RefreshPackageInformationAsync();
            Assert.IsType<InvalidOperationException>(service.PackageSnapshot.Error);
            Assert.Empty(service.PackageSnapshot.InstalledPackages);
            Assert.Empty(NuGetPluginCatalog.GetLoadablePackageIds(
                testDirectory,
                hostMajorVersion: 7,
                hostAbstractionsVersion: NuGetVersion.Parse("1.0.0")));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task InstalledPackageCompatibilityFollowsValidationSetting()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "nuget-manifest.json"),
                JsonSerializer.Serialize(new InstalledManifest(
                    [new InstalledPackageInfo(
                        "Old.Plugin",
                        "1.0.0",
                        HostMajorVersion: 6,
                        AbstractionsVersionRange: "[1.0.0, 2.0.0)")])));
            using var handler = new InMemoryNuGetHandler();
            using var service = CreateService(
                handler,
                testDirectory,
                hostMajorVersion: 7);

            await service.RefreshPackageInformationAsync();
            var package = Assert.Single(service.PackageSnapshot.InstalledPackages);

            Assert.Equal(PluginCompatibility.ValidationDisabled, package.IsCompatible);
            Assert.Equal(6, package.HostMajorVersion);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task InstalledPackageCompatibilityChecksAbstractionsVersionRange()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(testDirectory, "nuget-manifest.json"),
                JsonSerializer.Serialize(new InstalledManifest(
                [
                    new InstalledPackageInfo(
                        "Range.Plugin",
                        "1.0.0",
                        HostMajorVersion: 7,
                        AbstractionsVersionRange: "[2.0.0, 3.0.0)"),
                ])));
            using var handler = new InMemoryNuGetHandler();
            var hostAbstractionsVersion = NuGetVersion.Parse("1.5.0");
            using var service = CreateService(
                handler,
                testDirectory,
                new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
                {
                    [NuGetPluginService.AbstractionsPackageId] = hostAbstractionsVersion,
                },
                hostMajorVersion: 7);

            await service.RefreshPackageInformationAsync();
            var package = Assert.Single(service.PackageSnapshot.InstalledPackages);
            var loadablePackages = NuGetPluginCatalog.GetLoadablePackageIds(
                testDirectory,
                hostMajorVersion: 7,
                hostAbstractionsVersion);

            Assert.Equal(PluginCompatibility.ValidationDisabled, package.IsCompatible);
            Assert.Equal(
                PluginCompatibility.ValidationDisabled,
                loadablePackages.Contains("Range.Plugin"));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task BackgroundServiceRefreshesPluginInformationWithoutOpeningSettings()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.SearchResults =
                [
                    CreatePackageSearchMetadata(
                        "Background.Plugin",
                        "Background Plugin",
                        null,
                        null,
                        null,
                        null),
                ];
            handler.AddMetadataVersions(
                "Background.Plugin",
                CreatePluginVersionMetadata("1.0.0"));
            using var service = CreateService(
                handler,
                testDirectory);
            var updated = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            service.PackageInformationUpdated += (_, _) => updated.TrySetResult();

            Assert.IsAssignableFrom<IHostedService>(service);
            await service.StartAsync(CancellationToken.None);
            try
            {
                await updated.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
            }

            Assert.Null(service.PackageSnapshot.Error);
            Assert.Equal(
                "Background.Plugin",
                Assert.Single(service.PackageSnapshot.Packages).Id);
            Assert.Equal(["tags:windowtranslator-plugin"], handler.RequestedSearchTerms);
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
                    [new InstalledPackageInfo(
                        "Installed.Plugin",
                        "1.2.3",
                        HostMajorVersion: 7,
                        AbstractionsVersionRange: "(, )")])),
                Encoding.UTF8);
            using var handler = new InMemoryNuGetHandler();
            handler.SearchException = new HttpRequestException("NuGet search failed.");
            using var service = CreateService(
                handler,
                testDirectory,
                hostMajorVersion: 7);
            var viewModel = new PluginStoreViewModel(
                service,
                NullLogger<PluginStoreViewModel>.Instance,
                dialogService: null!);

            await service.RefreshPackageInformationAsync();

            var package = Assert.Single(viewModel.Packages);
            Assert.Equal("Installed.Plugin", package.Id);
            Assert.Equal("1.2.3", package.InstalledVersion);
            Assert.True(package.IsInstalled);
            Assert.NotNull(viewModel.ErrorMessage);
            Assert.Equal(["tags:windowtranslator-plugin"], handler.RequestedSearchTerms);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task RefreshReportsMetadataFailuresAndKeepsSuccessfulPackages()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.SearchResults =
                [
                    CreatePackageSearchMetadata(
                        "Available.Plugin",
                        null,
                        null,
                        null,
                        null,
                        null),
                    CreatePackageSearchMetadata(
                        "Unavailable.Plugin",
                        null,
                        null,
                        null,
                        null,
                        null),
                ];
            handler.AddMetadataVersions(
                "Available.Plugin",
                CreatePluginVersionMetadata("1.0.0"));
            handler.AddMetadataException(
                "Unavailable.Plugin",
                new HttpRequestException("Metadata request failed."));
            using var service = CreateService(handler, testDirectory);

            await service.RefreshPackageInformationAsync();

            Assert.Equal(
                "Available.Plugin",
                Assert.Single(service.PackageSnapshot.Packages).Id);
            var error = Assert.IsType<AggregateException>(service.PackageSnapshot.Error);
            Assert.Contains(
                error.InnerExceptions,
                exception => exception.Message.Contains(
                    "Unavailable.Plugin",
                    StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task RefreshReportsAnErrorWhenEveryMetadataRequestFails()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.SearchResults =
                [
                    CreatePackageSearchMetadata(
                        "Unavailable.Plugin",
                        null,
                        null,
                        null,
                        null,
                        null),
                ];
            handler.AddMetadataException(
                "Unavailable.Plugin",
                new HttpRequestException("Metadata request failed."));
            using var service = CreateService(handler, testDirectory);

            await service.RefreshPackageInformationAsync();

            Assert.Empty(service.PackageSnapshot.Packages);
            Assert.NotNull(service.PackageSnapshot.Error);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task SearchReturnsReleaseAndPrereleaseVersions()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.SearchResults =
                [
                    CreatePackageSearchMetadata(
                        "Test.Plugin",
                        "Test Plugin",
                        "Test description",
                        "WindowTranslator.Tests",
                        null,
                        null,
                        iconUrl: "https://nuget.test/icons/test-plugin.png"),
                ];
            handler.AddMetadataVersions(
                "Test.Plugin",
                CreatePluginVersionMetadata("1.0.0"),
                CreatePluginVersionMetadata("1.1.0-beta.1"),
                CreatePluginVersionMetadata("1.1.0-beta.2"));
            using var service = CreateService(
                handler,
                testDirectory);

            await service.RefreshPackageInformationAsync();
            var package = Assert.Single(service.PackageSnapshot.Packages);

            Assert.Equal("Test.Plugin", package.Id);
            Assert.Equal(
                ["1.0.0", "1.1.0-beta.1", "1.1.0-beta.2"],
                package.Versions);
            Assert.Equal("https://nuget.test/icons/test-plugin.png", package.IconUrl);
            Assert.Equal([true], handler.RequestedPrereleaseOptions);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task DisclaimerPreferenceIsStoredInThePluginManifestAndPreservedByPluginOperations()
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
                        ["lib/net10.0/Root.Plugin.dll"] = "root"u8.ToArray(),
                    }));

            using (var service = CreateService(handler, testDirectory))
            {
                await service.SetHideDisclaimerAsync(true);
                await service.InstallPackageAsync("Root.Plugin", "1.0.0");
                await service.UninstallPackageAsync("Root.Plugin");
            }

            using (var service = CreateService(handler, testDirectory))
            {
                Assert.True(service.HideDisclaimer);
                await service.SetHideDisclaimerAsync(false);
            }

            using var reloadedService = CreateService(handler, testDirectory);
            Assert.False(reloadedService.HideDisclaimer);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task SearchUsesNuGetOwnersForOfficialPackageStatus()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.SearchResults =
                [
                    CreatePackageSearchMetadata(
                        "Official.Plugin",
                        title: null,
                        description: null,
                        authors: "Other",
                        projectUrl: null,
                        licenseUrl: null,
                        owners: [NuGetPluginService.OfficialPackageOwner]),
                    CreatePackageSearchMetadata(
                        "Spoofed.Plugin",
                        title: null,
                        description: null,
                        authors: NuGetPluginService.OfficialPackageOwner,
                        projectUrl: null,
                        licenseUrl: null,
                        owners: ["Other"]),
                ];
            handler.AddMetadataVersions(
                "Official.Plugin",
                CreatePluginVersionMetadata("1.0.0"));
            handler.AddMetadataVersions(
                "Spoofed.Plugin",
                CreatePluginVersionMetadata("1.0.0"));
            using var service = CreateService(handler, testDirectory);

            await service.RefreshPackageInformationAsync();

            Assert.True(service.PackageSnapshot.Packages
                .Single(package => package.Id == "Official.Plugin")
                .IsOfficial);
            Assert.False(service.PackageSnapshot.Packages
                .Single(package => package.Id == "Spoofed.Plugin")
                .IsOfficial);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task SearchKeepsOnlyVersionsWithCompatibleDirectAbstractionsDependency()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.SearchResults =
                [
                    CreatePackageSearchMetadata(
                        "Compatible.Plugin",
                        null,
                        null,
                        null,
                        null,
                        null),
                    CreatePackageSearchMetadata(
                        "Missing.Dependency.Plugin",
                        null,
                        null,
                        null,
                        null,
                        null),
                ];
            handler.AddMetadataVersions(
                "Compatible.Plugin",
                CreatePluginVersionMetadata("1.0.0", "[1.0.0, 2.0.0)"),
                CreatePluginVersionMetadata("2.0.0", "[2.0.0, 3.0.0)"));
            handler.AddMetadataVersions(
                "Missing.Dependency.Plugin",
                CreatePluginVersionMetadata(
                    "1.0.0",
                    includeAbstractionsDependency: false));

            using var service = CreateService(
                handler,
                testDirectory,
                new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WindowTranslator.Abstractions"] = NuGetVersion.Parse("1.5.0"),
                });

            await service.RefreshPackageInformationAsync();
            var package = Assert.Single(service.PackageSnapshot.Packages);

            Assert.Equal("Compatible.Plugin", package.Id);
            Assert.Equal(
                PluginCompatibility.ValidationDisabled ? ["1.0.0", "2.0.0"] : ["1.0.0"],
                package.Versions);
            Assert.Equal(["tags:windowtranslator-plugin"], handler.RequestedSearchTerms);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void PackageVersionSelectionRequiresOptInForPrerelease()
    {
        var package = new PluginPackageViewModel(
            new NuGetPackageInfo(
                "Test.Plugin",
                "Test Plugin",
                string.Empty,
                string.Empty,
                null,
                null,
                ["1.0.0", "1.1.0-beta.1", "1.1.0-beta.2"]),
            isInstalled: true,
            installedVersion: "1.0.0");

        Assert.Equal("1.0.0", package.LatestVersion);
        Assert.False(package.UsePrerelease);
        Assert.False(package.IsUpdateAvailable);

        package.UsePrerelease = true;

        Assert.Equal("1.1.0-beta.2", package.LatestVersion);
        Assert.True(package.IsUpdateAvailable);
        Assert.True(package.CanInstall);

        var prereleaseOnlyPackage = new PluginPackageViewModel(
            new NuGetPackageInfo(
                "Preview.Plugin",
                "Preview Plugin",
                string.Empty,
                string.Empty,
                null,
                null,
                ["2.0.0-preview.1"]),
            isInstalled: false,
            installedVersion: null);

        Assert.Null(prereleaseOnlyPackage.LatestVersion);
        Assert.False(prereleaseOnlyPackage.CanInstall);

        prereleaseOnlyPackage.UsePrerelease = true;

        Assert.Equal("2.0.0-preview.1", prereleaseOnlyPackage.LatestVersion);
        Assert.True(prereleaseOnlyPackage.CanInstall);

        var releaseNewerThanPrerelease = new PluginPackageViewModel(
            new NuGetPackageInfo(
                "Released.Plugin",
                "Released Plugin",
                string.Empty,
                string.Empty,
                null,
                null,
                ["1.9.0-preview.1", "2.0.0"]),
            isInstalled: false,
            installedVersion: null)
        {
            UsePrerelease = true,
        };

        Assert.Equal("2.0.0", releaseNewerThanPrerelease.LatestVersion);
    }

    [Fact]
    public void PackagePresentationUsesOfficialFlagFromMetadata()
    {
        var package = new PluginPackageViewModel(
            new NuGetPackageInfo(
                "Test.Plugin",
                "Test Plugin",
                "Description",
                "Other",
                null,
                null,
                ["1.0.0"],
                "https://nuget.test/icons/test-plugin.png",
                IsOfficial: true),
            isInstalled: false,
            installedVersion: null);

        Assert.Equal("Test Plugin", package.Title);
        Assert.True(package.IsOfficial);
        Assert.Equal("https://nuget.test/icons/test-plugin.png", package.IconUrl);
    }

    [Fact]
    public void IncompatibleInstalledPackageCanReinstallACompatibleVersion()
    {
        var package = new PluginPackageViewModel(
            new NuGetPackageInfo(
                "Test.Plugin",
                "Test Plugin",
                string.Empty,
                string.Empty,
                null,
                null,
                ["1.0.0"]),
            isInstalled: true,
            installedVersion: "1.0.0",
            isCompatible: false,
            hasCompatiblePackageVersion: true);

        Assert.False(package.IsUpdateAvailable);
        Assert.True(package.RequiresReinstall);
        Assert.True(package.CanUpdate);

        package.IsCompatible = true;

        Assert.False(package.RequiresReinstall);
        Assert.False(package.CanUpdate);
    }

    [Fact]
    public void RestartArgumentsAreNotForwardedToTheRestartedApplication()
    {
        var arguments = ApplicationRestart.RemoveRestartArguments(
        [
            "--IgnoreUpdate",
            ApplicationRestart.RestartProcessIdArgument,
            "1234",
            "--SuppressMode",
        ]);

        Assert.Equal(["--IgnoreUpdate", "--SuppressMode"], arguments);
    }

    [Fact]
    public async Task InstallCompatibilityFollowsValidationSetting()
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

            using var service = CreateService(
                handler,
                testDirectory,
                new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WindowTranslator.Abstractions"] = NuGetVersion.Parse("1.5.0"),
                });

            if (PluginCompatibility.ValidationDisabled)
            {
                await service.InstallPackageAsync("Root.Plugin", "2.0.0");
                Assert.True(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            }
            else
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.InstallPackageAsync("Root.Plugin", "2.0.0"));
                Assert.Contains("WindowTranslator.Abstractions", exception.Message);
                Assert.Contains("[2.0.0, 3.0.0)", exception.Message);
                Assert.False(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
                Assert.Empty(service.PackageSnapshot.InstalledPackages);
            }
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

            using var service = CreateService(
                handler,
                testDirectory,
                new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WindowTranslator.Abstractions"] = NuGetVersion.Parse("1.5.0"),
                });

            await service.InstallPackageAsync("Root.Plugin", "1.0.0");

            Assert.True(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            Assert.Equal(
                "[1.0.0, 2.0.0)",
                Assert.Single(service.PackageSnapshot.InstalledPackages)
                    .AbstractionsVersionRange);
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
    public async Task InstallRejectsPackageWithoutDirectAbstractionsDependency()
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
                        ["lib/net10.0/Root.Plugin.dll"] = "root"u8.ToArray(),
                    },
                    includeAbstractionsDependency: false));

            using var service = CreateService(handler, testDirectory);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.InstallPackageAsync("Root.Plugin", "1.0.0"));

            Assert.Contains("WindowTranslator.Abstractions", exception.Message);
            Assert.False(Directory.Exists(Path.Combine(testDirectory, "Root.Plugin")));
            Assert.Empty(service.PackageSnapshot.InstalledPackages);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task InstallRejectsPackageWithoutPluginTag()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var targetDirectory = Path.Combine(testDirectory, "Root.Plugin");
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(Path.Combine(targetDirectory, "plugin.txt"), "old");
            await NuGetPluginService.SaveManifestAsync(
                Path.Combine(testDirectory, "nuget-manifest.json"),
                new([new(
                    "Root.Plugin",
                    "0.9.0",
                    HostMajorVersion: 1,
                    AbstractionsVersionRange: "(, )")]),
                CancellationToken.None);
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
                        ["lib/net10.0/Root.Plugin.dll"] = "root"u8.ToArray(),
                    },
                    includePluginTag: false));

            using var service = CreateService(handler, testDirectory);
            await service.RefreshPackageInformationAsync();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.InstallPackageAsync("Root.Plugin", "1.0.0"));

            Assert.Contains("プラグインタグ", exception.Message);
            Assert.Equal("old", File.ReadAllText(Path.Combine(targetDirectory, "plugin.txt")));
            Assert.Equal("0.9.0", Assert.Single(service.PackageSnapshot.InstalledPackages).Version);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task SelectedPackageLoadsReadmeForTheSelectedReleaseChannel()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.AddPackage(
                "Readme.Plugin",
                "1.0.0",
                CreatePackage(
                    "Readme.Plugin",
                    "1.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Readme.Plugin.dll"] = "release"u8.ToArray(),
                        ["README.md"] = "# Release README"u8.ToArray(),
                    }));
            handler.AddPackage(
                "Readme.Plugin",
                "2.0.0-preview.1",
                CreatePackage(
                    "Readme.Plugin",
                    "2.0.0-preview.1",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Readme.Plugin.dll"] = "preview"u8.ToArray(),
                        ["README.md"] = "# Preview README"u8.ToArray(),
                    }));

            handler.AddReadmeUrl(
                "Readme.Plugin",
                "1.0.0",
                "https://nuget.test/readme/readme.plugin/1.0.0");
            handler.AddReadmeUrl(
                "Readme.Plugin",
                "2.0.0-preview.1",
                "https://nuget.test/readme/readme.plugin/2.0.0-preview.1");
            using var service = CreateService(
                handler,
                testDirectory);
            var viewModel = new PluginStoreViewModel(
                service,
                NullLogger<PluginStoreViewModel>.Instance,
                dialogService: null!);
            var package = new PluginPackageViewModel(
                new NuGetPackageInfo(
                    "Readme.Plugin",
                    "README Plugin",
                    string.Empty,
                    string.Empty,
                    null,
                    null,
                    ["1.0.0", "2.0.0-preview.1"]),
                isInstalled: false,
                installedVersion: null);

            viewModel.SelectedPackage = package;
            await WaitForReadmeAsync(package, "# Release README");

            package.UsePrerelease = true;
            await WaitForReadmeAsync(package, "# Preview README");

            Assert.Contains(
                handler.RequestedPaths,
                path => path.Equals("/readme/readme.plugin/1.0.0", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                handler.RequestedPaths,
                path => path.Equals(
                    "/readme/readme.plugin/2.0.0-preview.1",
                    StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                handler.RequestedPaths,
                path => path.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public async Task PackageReadmeUsesTheRequestedUiCulture()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            using var handler = new InMemoryNuGetHandler();
            handler.AddPackage(
                "Localized.Plugin",
                "1.0.0",
                CreatePackage(
                    "Localized.Plugin",
                    "1.0.0",
                    [],
                    new Dictionary<string, byte[]>
                    {
                        ["lib/net10.0/Localized.Plugin.dll"] = "plugin"u8.ToArray(),
                        ["README.md"] = """
                            ## ja

                            # 日本語

                            ## en

                            # English
                            """u8.ToArray(),
                    }));
            handler.AddReadmeUrl(
                "Localized.Plugin",
                "1.0.0",
                "https://nuget.test/readme/localized.plugin/1.0.0");
            using var service = CreateService(handler, testDirectory);

            var readme = await service.GetPackageReadmeAsync(
                "Localized.Plugin",
                "1.0.0",
                CultureInfo.GetCultureInfo("en-US"));

            Assert.Equal("# English", readme);
            Assert.DoesNotContain(
                handler.RequestedPaths,
                path => path.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void StartupCleanupDeletesDirectoriesMissingFromAReadableManifest()
    {
        var sourceDirectory = CreateTestDirectory();
        try
        {
            var installedDirectory = Path.Combine(sourceDirectory, "Installed.Plugin");
            var removedDirectory = Path.Combine(sourceDirectory, "Removed.Plugin");
            var interruptedInstallDirectory = Path.Combine(
                sourceDirectory,
                "Installed.Plugin.installing-test");
            Directory.CreateDirectory(installedDirectory);
            Directory.CreateDirectory(removedDirectory);
            Directory.CreateDirectory(interruptedInstallDirectory);
            File.WriteAllText(
                Path.Combine(sourceDirectory, "nuget-manifest.json"),
                JsonSerializer.Serialize(
                    new InstalledManifest(
                    [
                        new(
                            "Installed.Plugin",
                            "1.0.0",
                            HostMajorVersion: 1,
                            AbstractionsVersionRange: "(, )"),
                    ]),
                    NuGetPluginService.ManifestJsonOptions));

            NuGetPluginCatalog.DeleteUninstalledPackageDirectories(sourceDirectory);

            Assert.True(Directory.Exists(installedDirectory));
            Assert.False(Directory.Exists(removedDirectory));
            Assert.False(Directory.Exists(interruptedInstallDirectory));

            var directoryKeptForInvalidManifest = Path.Combine(
                sourceDirectory,
                "Kept.For.Invalid.Manifest");
            Directory.CreateDirectory(directoryKeptForInvalidManifest);
            File.WriteAllText(
                Path.Combine(sourceDirectory, "nuget-manifest.json"),
                "{ invalid json");

            NuGetPluginCatalog.DeleteUninstalledPackageDirectories(sourceDirectory);

            Assert.True(Directory.Exists(directoryKeptForInvalidManifest));
        }
        finally
        {
            DeleteTestDirectory(sourceDirectory);
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
            Directory.CreateDirectory(Path.Combine(sourceDirectory, "Root.Plugin.installing-test"));
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
            var includedPackages = new HashSet<string>(
                ["Root.Plugin", "Empty.Plugin"],
                StringComparer.OrdinalIgnoreCase);
            NuGetPluginCatalog.SynchronizePluginFiles(
                sourceDirectory,
                destinationDirectory,
                includedPackages);
            using var synchronizedFileLock = new FileStream(
                destinationPluginPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            NuGetPluginCatalog.SynchronizePluginFiles(
                sourceDirectory,
                destinationDirectory,
                includedPackages);

            Assert.False(File.Exists(Path.Combine(destinationDirectory, "Legacy.Plugin.dll")));
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
                Path.Combine(destinationDirectory, "Root.Plugin.installing-test")));
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
                destinationDirectory,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            Assert.Empty(Directory.EnumerateFileSystemEntries(destinationDirectory));
        }
        finally
        {
            DeleteTestDirectory(sourceDirectory);
            DeleteTestDirectory(destinationDirectory);
        }
    }

    [Fact]
    public void CatalogSynchronizationFollowsCompatibilityValidationSetting()
    {
        var sourceDirectory = CreateTestDirectory();
        var destinationDirectory = CreateTestDirectory();
        try
        {
            var compatibleDirectory = Path.Combine(sourceDirectory, "Compatible.Plugin");
            var incompatibleDirectory = Path.Combine(sourceDirectory, "Incompatible.Plugin");
            var orphanDirectory = Path.Combine(sourceDirectory, "Orphan.Plugin");
            Directory.CreateDirectory(compatibleDirectory);
            Directory.CreateDirectory(incompatibleDirectory);
            Directory.CreateDirectory(orphanDirectory);
            File.WriteAllText(Path.Combine(compatibleDirectory, "Compatible.Plugin.dll"), "compatible");
            File.WriteAllText(Path.Combine(incompatibleDirectory, "Incompatible.Plugin.dll"), "incompatible");
            File.WriteAllText(Path.Combine(orphanDirectory, "Orphan.Plugin.dll"), "orphan");
            Directory.CreateDirectory(Path.Combine(destinationDirectory, "Incompatible.Plugin"));
            File.WriteAllText(
                Path.Combine(destinationDirectory, "Incompatible.Plugin", "Incompatible.Plugin.dll"),
                "stale");
            File.WriteAllText(
                Path.Combine(sourceDirectory, "nuget-manifest.json"),
                JsonSerializer.Serialize(new InstalledManifest(
                [
                    new InstalledPackageInfo(
                        "Compatible.Plugin",
                        "1.0.0",
                        HostMajorVersion: 7,
                        AbstractionsVersionRange: "(, )"),
                    new InstalledPackageInfo(
                        "Incompatible.Plugin",
                        "1.0.0",
                        HostMajorVersion: 6,
                        AbstractionsVersionRange: "(, )"),
                ])));

            var loadablePackages = NuGetPluginCatalog.GetLoadablePackageIds(
                sourceDirectory,
                hostMajorVersion: 7,
                hostAbstractionsVersion: NuGetVersion.Parse("1.0.0"));
            NuGetPluginCatalog.SynchronizePluginFiles(
                sourceDirectory,
                destinationDirectory,
                loadablePackages);

            Assert.True(File.Exists(Path.Combine(
                destinationDirectory,
                "Compatible.Plugin",
                "Compatible.Plugin.dll")));
            Assert.Equal(
                PluginCompatibility.ValidationDisabled,
                Directory.Exists(Path.Combine(destinationDirectory, "Incompatible.Plugin")));
            Assert.False(Directory.Exists(Path.Combine(destinationDirectory, "Orphan.Plugin")));
            Assert.Equal(
                PluginCompatibility.ValidationDisabled
                    ? ["Compatible.Plugin", "Incompatible.Plugin"]
                    : ["Compatible.Plugin"],
                loadablePackages.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestDirectory(sourceDirectory);
            DeleteTestDirectory(destinationDirectory);
        }
    }

    [Fact]
    public async Task PrioritizedCatalogReplacesFallbackAssemblyAndKeepsSameTypeNameFromOtherAssembly()
    {
        var replacedAssemblyName = $"Replaced.Plugin.{Guid.NewGuid():N}";
        var nugetTypes = CreatePluginTypes(
            replacedAssemblyName,
            "NuGet.ReplacedPlugin",
            "NuGet.NuGetOnlyPlugin");
        var replacedBundledTypes = CreatePluginTypes(
            replacedAssemblyName,
            "Bundled.ReplacedPlugin",
            "Bundled.AlsoReplacedPlugin");
        var duplicateNugetTypes = CreatePluginTypes(
            replacedAssemblyName,
            "DuplicateNuGet.ReplacedPlugin",
            "DuplicateNuGet.AlsoReplacedPlugin");
        var bundledOnlyType = CreatePluginTypes(
            $"Bundled.Plugin.{Guid.NewGuid():N}",
            "Bundled.BundledOnlyPlugin")[0];
        var sameTypeNameFromOtherAssembly = CreatePluginTypes(
            $"Other.Plugin.{Guid.NewGuid():N}",
            "Other.ReplacedPlugin")[0];
        var nugetCatalog = new TestPluginCatalog(
            nugetTypes[0],
            nugetTypes[1],
            duplicateNugetTypes[0],
            duplicateNugetTypes[1]);
        var bundledCatalog = new TestPluginCatalog(
            replacedBundledTypes[0],
            replacedBundledTypes[1],
            bundledOnlyType,
            sameTypeNameFromOtherAssembly);
        var catalog = new PrioritizedPluginCatalog(nugetCatalog, bundledCatalog);

        await catalog.Initialize();

        Assert.True(catalog.IsInitialized);
        Assert.Equal(
            [
                nugetTypes[0],
                nugetTypes[1],
                bundledOnlyType,
                sameTypeNameFromOtherAssembly,
            ],
            catalog.GetPlugins().Select(plugin => plugin.Type));
        Assert.Equal(
            nugetTypes[0],
            catalog.Get("ReplacedPlugin", new Version(1, 0)).Type);
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
            await NuGetPluginService.SaveManifestAsync(
                Path.Combine(sourceDirectory, "nuget-manifest.json"),
                new([new(
                    "Catalog.Probe",
                    "1.0.0",
                    HostMajorVersion: 1,
                    AbstractionsVersionRange: "(, )")]),
                CancellationToken.None);

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
                hostMajorVersion: 1,
                hostAbstractionsVersion: NuGetVersion.Parse("1.0.0"),
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
            await NuGetPluginService.SaveManifestAsync(
                Path.Combine(sourceDirectory, "nuget-manifest.json"),
                new([new(
                    "Catalog.Probe",
                    "1.0.0",
                    HostMajorVersion: 1,
                    AbstractionsVersionRange: "(, )")]),
                CancellationToken.None);

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
                hostMajorVersion: 1,
                hostAbstractionsVersion: NuGetVersion.Parse("1.0.0"),
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
        InMemoryNuGetHandler handler,
        string pluginDirectory,
        IReadOnlyDictionary<string, NuGetVersion>? hostPackageVersions = null,
        int? hostMajorVersion = null)
        => new(
            NullLogger<NuGetPluginService>.Instance,
            new InMemoryHttpClientFactory(handler),
            handler.CreateRepository(),
            pluginDirectory,
            hostPackageVersions ?? NuGetPluginService.CreateHostPackageVersions(),
            hostMajorVersion ?? AppInfo.Instance.Version.Major);

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => this.Values.Add(value);
    }

    private static TestPackageVersion CreatePluginVersionMetadata(
        string version,
        string? abstractionsRange = null,
        bool includeAbstractionsDependency = true)
        => new(
            NuGetVersion.Parse(version),
            IsListed: true,
            [
                new PackageDependencyGroup(
                    NuGetFramework.ParseFolder("net10.0"),
                    includeAbstractionsDependency
                        ? [
                            new PackageDependency(
                                NuGetPluginService.AbstractionsPackageId,
                                abstractionsRange is null
                                    ? VersionRange.All
                                    : VersionRange.Parse(abstractionsRange)),
                        ]
                        : []),
            ]);

    private static IPackageSearchMetadata CreatePackageSearchMetadata(
        string packageId,
        string? title,
        string? description,
        string? authors,
        string? projectUrl,
        string? licenseUrl,
        NuGetVersion? version = null,
        IEnumerable<PackageDependencyGroup>? dependencySets = null,
        bool isListed = true,
        string? readmeFileUrl = null,
        string? iconUrl = null,
        IReadOnlyList<string>? owners = null)
        => new TestPackageSearchMetadata
        {
            Identity = new PackageIdentity(
                packageId,
                version ?? NuGetVersion.Parse("0.0.0")),
            Title = title!,
            Description = description!,
            Authors = authors!,
            ProjectUrl = projectUrl is null ? null! : new Uri(projectUrl),
            LicenseUrl = licenseUrl is null ? null! : new Uri(licenseUrl),
            DependencySets = dependencySets ?? [],
            IsListed = isListed,
            ReadmeFileUrl = readmeFileUrl!,
            IconUrl = iconUrl is null ? null! : new Uri(iconUrl),
            OwnersList = owners ?? [],
        };

    private static async Task WaitForReadmeAsync(
        PluginPackageViewModel package,
        string expectedReadme)
    {
        if (package.ReadmeMarkdown == expectedReadme)
        {
            return;
        }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        PropertyChangedEventHandler? handler = null;
        handler = (_, e) =>
        {
            if (e.PropertyName == nameof(PluginPackageViewModel.ReadmeMarkdown)
                && package.ReadmeMarkdown == expectedReadme)
            {
                completion.TrySetResult();
            }
        };
        package.PropertyChanged += handler;
        try
        {
            if (package.ReadmeMarkdown == expectedReadme)
            {
                return;
            }
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            package.PropertyChanged -= handler;
        }
    }

    private static byte[] CreatePackage(
        string id,
        string version,
        IReadOnlyCollection<TestDependency> dependencies,
        IReadOnlyDictionary<string, byte[]> entries,
        bool includePluginTag = true,
        bool includeAbstractionsDependency = true)
    {
        var packageDependencies = dependencies.ToList();
        if (includeAbstractionsDependency
            && !packageDependencies.Any(dependency => dependency.Id.Equals(
                "WindowTranslator.Abstractions",
                StringComparison.OrdinalIgnoreCase)))
        {
            packageDependencies.Add(new(
                "WindowTranslator.Abstractions",
                "(, )",
                Exclude: "Runtime"));
        }

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var dependencyElements = packageDependencies.Select(dependency =>
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
            var metadata = new XElement(
                "metadata",
                new XElement("id", id),
                new XElement("version", version),
                new XElement("authors", "WindowTranslator.Tests"),
                new XElement("description", "Test package"));
            if (includePluginTag)
            {
                metadata.Add(new XElement("tags", "windowtranslator-plugin"));
            }
            if (entries.ContainsKey("README.md"))
            {
                metadata.Add(new XElement("readme", "README.md"));
            }
            metadata.Add(new XElement(
                "dependencies",
                new XElement(
                    "group",
                    new XAttribute("targetFramework", "net10.0"),
                    dependencyElements)));
            var nuspec = new XDocument(new XElement("package", metadata));
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

    private sealed record TestPackageVersion(
        NuGetVersion Version,
        bool IsListed,
        IReadOnlyList<PackageDependencyGroup> DependencyGroups);

    private sealed class TestPackageSearchMetadata : IPackageSearchMetadata
    {
        public string Authors { get; init; } = null!;
        public IEnumerable<PackageDependencyGroup> DependencySets { get; init; } = [];
        public string Description { get; init; } = null!;
        public long? DownloadCount { get; init; }
        public Uri IconUrl { get; init; } = null!;
        public PackageIdentity Identity { get; init; } = null!;
        public Uri LicenseUrl { get; init; } = null!;
        public Uri ProjectUrl { get; init; } = null!;
        public Uri ReadmeUrl { get; init; } = null!;
        public string ReadmeFileUrl { get; init; } = null!;
        public Uri ReportAbuseUrl { get; init; } = null!;
        public Uri PackageDetailsUrl { get; init; } = null!;
        public DateTimeOffset? Published { get; init; }
        public IReadOnlyList<string> OwnersList { get; init; } = [];
        public string Owners { get; init; } = null!;
        public bool RequireLicenseAcceptance { get; init; }
        public string Summary { get; init; } = null!;
        public string Tags { get; init; } = null!;
        public string Title { get; init; } = null!;
        public bool IsListed { get; init; }
        public bool PrefixReserved { get; init; }
        public LicenseMetadata LicenseMetadata { get; init; } = null!;
        public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities { get; init; } = [];

        public Task<PackageDeprecationMetadata?> GetDeprecationMetadataAsync()
            => Task.FromResult<PackageDeprecationMetadata?>(null);

        public Task<IEnumerable<VersionInfo>> GetVersionsAsync()
            => Task.FromResult<IEnumerable<VersionInfo>>([]);
    }

    private sealed class TestPluginCatalog : IPluginCatalog
    {
        private readonly List<Plugin> plugins;

        public TestPluginCatalog(params Type[] pluginTypes)
        {
            this.plugins = pluginTypes
                .Select(type => new Plugin(
                    type.Assembly,
                    type,
                    type.Name,
                    new Version(1, 0),
                    this,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    []))
                .ToList();
        }

        public bool IsInitialized { get; private set; }

        public Task Initialize()
        {
            this.IsInitialized = true;
            return Task.CompletedTask;
        }

        public List<Plugin> GetPlugins() => [.. this.plugins];

        public Plugin Get(string name, Version version)
            => this.plugins.FirstOrDefault(plugin =>
                plugin.Name == name && plugin.Version == version)!;
    }

    private static Type[] CreatePluginTypes(string assemblyName, params string[] typeNames)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName);
        return typeNames
            .Select(typeName => module
                .DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class)
                .CreateType()!)
            .ToArray();
    }

    private sealed class InMemoryHttpClientFactory(InMemoryNuGetHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class InMemoryNuGetHandler : HttpMessageHandler
    {
        private readonly Dictionary<(string Id, string Version), byte[]> packages = new();
        private readonly Dictionary<string, IReadOnlyList<TestPackageVersion>> metadataVersions =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Exception> metadataExceptions =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> readmeUrls =
            new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<IPackageSearchMetadata> SearchResults { get; set; } = [];

        public Exception? SearchException { get; set; }

        public List<string> RequestedSearchTerms { get; } = [];

        public List<bool> RequestedPrereleaseOptions { get; } = [];

        public List<string> RequestedPaths { get; } = [];

        public void AddPackage(string id, string version, byte[] package)
            => this.packages[(NormalizeId(id), NormalizeVersion(version))] = package;

        public void AddMetadataVersions(string packageId, params TestPackageVersion[] packageVersions)
            => this.metadataVersions[packageId] = packageVersions;

        public void AddMetadataException(string packageId, Exception exception)
            => this.metadataExceptions[packageId] = exception;

        public void AddReadmeUrl(string packageId, string version, string url)
            => this.readmeUrls[GetReadmeKey(packageId, NuGetVersion.Parse(version))] = url;

        public SourceRepository CreateRepository()
            => new(
                new PackageSource("https://nuget.test/v3/index.json"),
                [
                    new InMemoryResourceProvider<PackageSearchResource>(
                        new InMemoryPackageSearchResource(this)),
                    new InMemoryResourceProvider<PackageMetadataResource>(
                        new InMemoryPackageMetadataResource(this)),
                    new InMemoryResourceProvider<FindPackageByIdResource>(
                        new InMemoryFindPackageByIdResource(this)),
                ]);

        private static string GetReadmeKey(string packageId, NuGetVersion version)
            => $"{packageId}\n{version.ToNormalizedString()}";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            this.RequestedPaths.Add(path);

            var segments = path.Trim('/').Split('/');
            if (segments.Length == 3
                && segments[0].Equals("readme", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateReadmeResponse(segments[1], segments[2]));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private HttpResponseMessage CreateReadmeResponse(string packageId, string version)
        {
            if (!this.packages.TryGetValue(
                    (NormalizeId(packageId), NormalizeVersion(version)),
                    out var package))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            using var stream = new MemoryStream(package);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var readmeEntry = archive.Entries.FirstOrDefault(entry =>
                entry.FullName.Equals("README.md", StringComparison.OrdinalIgnoreCase));
            if (readmeEntry is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            using var reader = new StreamReader(readmeEntry.Open(), Encoding.UTF8);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reader.ReadToEnd(), Encoding.UTF8, "text/markdown"),
            };
        }

        private static string NormalizeId(string packageId) => packageId.ToLowerInvariant();

        private static string NormalizeVersion(string version)
            => NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();

        private sealed class InMemoryPackageSearchResource(InMemoryNuGetHandler source)
            : PackageSearchResource
        {
            public override Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(
                string searchTerm,
                SearchFilter filters,
                int skip,
                int take,
                NuGet.Common.ILogger log,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                source.RequestedSearchTerms.Add(searchTerm);
                source.RequestedPrereleaseOptions.Add(filters.IncludePrerelease);
                return source.SearchException is null
                    ? Task.FromResult(source.SearchResults.Skip(skip).Take(take).AsEnumerable())
                    : Task.FromException<IEnumerable<IPackageSearchMetadata>>(source.SearchException);
            }
        }

        private sealed class InMemoryPackageMetadataResource(InMemoryNuGetHandler source)
            : PackageMetadataResource
        {
            public override Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
                string packageId,
                bool includePrerelease,
                bool includeUnlisted,
                SourceCacheContext sourceCacheContext,
                NuGet.Common.ILogger log,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (source.metadataExceptions.TryGetValue(packageId, out var exception))
                {
                    return Task.FromException<IEnumerable<IPackageSearchMetadata>>(exception);
                }
                var versions = source.metadataVersions.TryGetValue(packageId, out var packageVersions)
                    ? packageVersions
                    : [];
                var metadata = versions
                    .Where(version => includePrerelease || !version.Version.IsPrerelease)
                    .Where(version => includeUnlisted || version.IsListed)
                    .Select(version => CreatePackageSearchMetadata(
                        packageId,
                        title: null,
                        description: null,
                        authors: null,
                        projectUrl: null,
                        licenseUrl: null,
                        version.Version,
                        version.DependencyGroups,
                        version.IsListed));
                return Task.FromResult(metadata);
            }

            public override Task<IPackageSearchMetadata> GetMetadataAsync(
                PackageIdentity identity,
                SourceCacheContext sourceCacheContext,
                NuGet.Common.ILogger log,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                source.readmeUrls.TryGetValue(GetReadmeKey(identity.Id, identity.Version), out var readmeUrl);
                return Task.FromResult(CreatePackageSearchMetadata(
                    identity.Id,
                    title: null,
                    description: null,
                    authors: null,
                    projectUrl: null,
                    licenseUrl: null,
                    identity.Version,
                    readmeFileUrl: readmeUrl));
            }
        }

        private sealed class InMemoryFindPackageByIdResource(InMemoryNuGetHandler source)
            : FindPackageByIdResource
        {
            public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
                string id,
                SourceCacheContext cacheContext,
                NuGet.Common.ILogger logger,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var normalizedId = NormalizeId(id);
                source.RequestedPaths.Add($"/v3-flatcontainer/{normalizedId}/index.json");
                return Task.FromResult(source.packages.Keys
                    .Where(key => key.Id == normalizedId)
                    .Select(key => NuGetVersion.Parse(key.Version))
                    .OrderBy(version => version)
                    .AsEnumerable());
            }

            public override async Task<bool> CopyNupkgToStreamAsync(
                string id,
                NuGetVersion version,
                Stream destination,
                SourceCacheContext cacheContext,
                NuGet.Common.ILogger logger,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var normalizedId = NormalizeId(id);
                var normalizedVersion = NormalizeVersion(version.ToNormalizedString());
                source.RequestedPaths.Add(
                    $"/v3-flatcontainer/{normalizedId}/{normalizedVersion}/{normalizedId}.{normalizedVersion}.nupkg");
                if (!source.packages.TryGetValue((normalizedId, normalizedVersion), out var package))
                {
                    return false;
                }

                await destination.WriteAsync(package, token);
                return true;
            }

            public override Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
                string id,
                NuGetVersion version,
                SourceCacheContext cacheContext,
                NuGet.Common.ILogger logger,
                CancellationToken token)
                => throw new NotSupportedException();

            public override Task<IPackageDownloader> GetPackageDownloaderAsync(
                PackageIdentity packageIdentity,
                SourceCacheContext cacheContext,
                NuGet.Common.ILogger logger,
                CancellationToken token)
                => throw new NotSupportedException();

            public override Task<bool> DoesPackageExistAsync(
                string id,
                NuGetVersion version,
                SourceCacheContext cacheContext,
                NuGet.Common.ILogger logger,
                CancellationToken token)
                => Task.FromResult(source.packages.ContainsKey(
                    (NormalizeId(id), NormalizeVersion(version.ToNormalizedString()))));
        }
    }

    private sealed class InMemoryResourceProvider<TResource>(TResource resource)
        : ResourceProvider(typeof(TResource))
        where TResource : class, INuGetResource
    {
        public override Task<Tuple<bool, INuGetResource?>> TryCreate(
            SourceRepository source,
            CancellationToken token)
            => Task.FromResult(Tuple.Create<bool, INuGetResource?>(true, resource));
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
