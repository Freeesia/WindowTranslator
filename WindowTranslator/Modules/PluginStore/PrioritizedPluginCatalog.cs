using System.Reflection;
using Weikio.PluginFramework.Abstractions;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// 優先カタログのプラグインを先に返し、同じアセンブリ名のフォールバックプラグインを除外します。
/// </summary>
internal sealed class PrioritizedPluginCatalog(
    IPluginCatalog preferredCatalog,
    IPluginCatalog fallbackCatalog) : IPluginCatalog
{
    private readonly IPluginCatalog preferredCatalog = preferredCatalog;
    private readonly IPluginCatalog fallbackCatalog = fallbackCatalog;

    /// <inheritdoc/>
    public bool IsInitialized
        => this.preferredCatalog.IsInitialized && this.fallbackCatalog.IsInitialized;

    /// <inheritdoc/>
    public async Task Initialize()
    {
        await this.preferredCatalog.Initialize().ConfigureAwait(false);
        await this.fallbackCatalog.Initialize().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public List<Plugin> GetPlugins()
    {
        var selectedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return SelectPluginsByAssembly(
                this.preferredCatalog.GetPlugins(),
                selectedAssemblyNames)
            .Concat(SelectPluginsByAssembly(
                this.fallbackCatalog.GetPlugins(),
                selectedAssemblyNames))
            .ToList();
    }

    private static List<Plugin> SelectPluginsByAssembly(
        List<Plugin> plugins,
        HashSet<string> selectedAssemblyNames)
    {
        var selectedAssemblies = new HashSet<Assembly>(ReferenceEqualityComparer.Instance);
        foreach (var plugin in plugins)
        {
            var assembly = plugin.Type.Assembly;
            if (selectedAssemblies.Contains(assembly))
            {
                continue;
            }

            var assemblyName = assembly.GetName().Name;
            if (assemblyName is null || selectedAssemblyNames.Add(assemblyName))
            {
                selectedAssemblies.Add(assembly);
            }
        }

        return plugins
            .Where(plugin => selectedAssemblies.Contains(plugin.Type.Assembly))
            .ToList();
    }

    /// <inheritdoc/>
    public Plugin Get(string name, Version version)
        => this.GetPlugins().FirstOrDefault(plugin =>
            plugin.Name == name && plugin.Version == version)!;
}
