using NuGet.Versioning;

namespace WindowTranslator.Modules.PluginStore;

internal static class PluginCompatibility
{
#if DISABLE_PLUGIN_COMPATIBILITY_VALIDATION
    internal static bool ValidationDisabled => true;
#else
    internal static bool ValidationDisabled => false;
#endif

    internal static bool IsVersionCompatible(VersionRange? requiredVersion, NuGetVersion? hostVersion)
        => ValidationDisabled
            || (hostVersion is not null && requiredVersion?.Satisfies(hostVersion) is not false);

    internal static bool IsHostMajorCompatible(int installedHostMajorVersion, int hostMajorVersion)
        => ValidationDisabled
            || installedHostMajorVersion == hostMajorVersion;

    internal static bool IsInstalledPackageCompatible(
        int installedHostMajorVersion,
        int hostMajorVersion,
        string? abstractionsVersionRange,
        NuGetVersion? hostAbstractionsVersion)
    {
        if (ValidationDisabled)
        {
            return true;
        }

        return IsHostMajorCompatible(installedHostMajorVersion, hostMajorVersion)
            && !string.IsNullOrWhiteSpace(abstractionsVersionRange)
            && VersionRange.TryParse(abstractionsVersionRange, out var requiredVersion)
            && IsVersionCompatible(requiredVersion, hostAbstractionsVersion);
    }
}
