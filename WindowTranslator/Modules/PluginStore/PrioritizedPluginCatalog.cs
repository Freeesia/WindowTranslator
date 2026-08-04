using Weikio.PluginFramework.Abstractions;

namespace WindowTranslator.Modules.PluginStore;

/// <summary>
/// 優先カタログのプラグインを先に返し、同じ型名のフォールバックプラグインを除外します。
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
        var typeNames = new HashSet<string>(StringComparer.Ordinal);
        return this.preferredCatalog
            .GetPlugins()
            .Concat(this.fallbackCatalog.GetPlugins())
            .Where(plugin => typeNames.Add(plugin.Type.Name))
            .ToList();
    }

    /// <inheritdoc/>
    public Plugin Get(string name, Version version)
        => this.GetPlugins().FirstOrDefault(plugin =>
            plugin.Name == name && plugin.Version == version)!;
}
