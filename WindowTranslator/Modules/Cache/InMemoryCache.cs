using System.Collections.Generic;

namespace WindowTranslator.Modules.Cache;

public class InMemoryCache : ICacheModule
{
    private readonly Dictionary<string,string> cache = new();

    public bool Contains(string src)
        => this.cache.ContainsKey(src);
    public void AddRange(IEnumerable<(string src, string dst)> pairs)
    {
        foreach (var (src, dst) in pairs)
        {
            this.cache.Add(src, dst);
        }
    }
    public string Get(string src)
        => this.cache[src];
}