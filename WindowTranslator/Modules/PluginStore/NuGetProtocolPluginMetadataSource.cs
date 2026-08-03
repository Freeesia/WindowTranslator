using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace WindowTranslator.Modules.PluginStore;

internal interface INuGetPluginMetadataSource
{
    Task<IReadOnlyList<NuGetPluginSearchMetadata>> SearchAsync(
        string tag,
        bool includePrerelease,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NuGetPluginVersionMetadata>> GetPackageVersionsAsync(
        string packageId,
        CancellationToken cancellationToken);

    Task<string?> GetReadmeUrlAsync(
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken);
}

internal sealed class NuGetProtocolPluginMetadataSource(string serviceIndexUrl) : INuGetPluginMetadataSource
{
    private const int SearchResultLimit = 100;

    private readonly SourceRepository repository = Repository.Factory.GetCoreV3(serviceIndexUrl);

    public async Task<IReadOnlyList<NuGetPluginSearchMetadata>> SearchAsync(
        string tag,
        bool includePrerelease,
        CancellationToken cancellationToken)
    {
        var searchResource = await this.repository
            .GetResourceAsync<PackageSearchResource>(cancellationToken)
            .ConfigureAwait(false);
        var searchFilter = new SearchFilter(includePrerelease)
        {
            IncludeDelisted = false,
        };
        var results = await searchResource.SearchAsync(
            $"tags:{tag}",
            searchFilter,
            skip: 0,
            take: SearchResultLimit,
            NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);

        return results
            .Where(metadata => !string.IsNullOrWhiteSpace(metadata.Identity?.Id))
            .Select(metadata => new NuGetPluginSearchMetadata(
                metadata.Identity.Id,
                metadata.Title,
                metadata.Description,
                metadata.Authors,
                metadata.ProjectUrl?.AbsoluteUri,
                metadata.LicenseUrl?.AbsoluteUri))
            .ToArray();
    }

    public async Task<IReadOnlyList<NuGetPluginVersionMetadata>> GetPackageVersionsAsync(
        string packageId,
        CancellationToken cancellationToken)
    {
        var metadataResource = await this.repository
            .GetResourceAsync<PackageMetadataResource>(cancellationToken)
            .ConfigureAwait(false);
        using var cacheContext = new SourceCacheContext();
        var versions = await metadataResource.GetMetadataAsync(
            packageId,
            includePrerelease: true,
            includeUnlisted: false,
            cacheContext,
            NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);

        return versions
            .Where(metadata => metadata.Identity?.Version is not null)
            .Select(metadata => new NuGetPluginVersionMetadata(
                metadata.Identity.Version,
                metadata.IsListed,
                metadata.DependencySets?.ToArray() ?? []))
            .ToArray();
    }

    public async Task<string?> GetReadmeUrlAsync(
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken)
    {
        var metadataResource = await this.repository
            .GetResourceAsync<PackageMetadataResource>(cancellationToken)
            .ConfigureAwait(false);
        using var cacheContext = new SourceCacheContext();
        var metadata = await metadataResource.GetMetadataAsync(
            new PackageIdentity(packageId, version),
            cacheContext,
            NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(metadata?.ReadmeFileUrl)
            ? null
            : metadata.ReadmeFileUrl;
    }
}

internal sealed record NuGetPluginSearchMetadata(
    string Id,
    string? Title,
    string? Description,
    string? Authors,
    string? ProjectUrl,
    string? LicenseUrl);

internal sealed record NuGetPluginVersionMetadata(
    NuGetVersion Version,
    bool IsListed,
    IReadOnlyList<PackageDependencyGroup> DependencyGroups);
