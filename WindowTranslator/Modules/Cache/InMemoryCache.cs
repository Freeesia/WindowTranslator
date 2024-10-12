using System.Collections.Concurrent;
using WindowTranslator.ComponentModel;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.Cache;

[LocalizedDisplayName(typeof(Resources), nameof(InMemoryCache))]
[DefaultModule]
public class InMemoryCache : ICacheModule
{
    private readonly ConcurrentDictionary<string, string> cache = new();

    public bool Contains(string src)
        => this.cache.ContainsKey(src);
    public void AddRange(IEnumerable<(string src, string dst)> pairs)
    {
        foreach (var (src, dst) in pairs)
        {
            this.cache.AddOrUpdate(src, dst, (_, _) => dst);
        }
    }
    public string Get(string src)
        => this.cache.TryGetValue(src, out var dst) ? dst : string.Empty;
}