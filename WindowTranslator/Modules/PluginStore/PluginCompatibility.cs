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

    internal static bool IsHostMajorCompatible(int? installedHostMajorVersion, int hostMajorVersion)
        => ValidationDisabled
            || installedHostMajorVersion is null
            || installedHostMajorVersion == hostMajorVersion;
}
