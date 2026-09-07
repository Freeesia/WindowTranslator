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
        var plugins = this.preferredCatalog.GetPlugins()
            .Concat(this.fallbackCatalog.GetPlugins())
            .ToList();
        var selectedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selectedAssemblies = new HashSet<Assembly>(ReferenceEqualityComparer.Instance);
        foreach (var assembly in plugins
                     .Select(plugin => plugin.Type.Assembly)
                     .Distinct<Assembly>(ReferenceEqualityComparer.Instance))
        {
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
