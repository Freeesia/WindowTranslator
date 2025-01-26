using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Quickenshtein;
using WindowTranslator.ComponentModel;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.Cache;

[LocalizedDisplayName(typeof(Resources), nameof(InMemoryCache))]
public class InMemoryCache(ILogger<InMemoryCache> logger) : ICacheModule
{
    private readonly ConcurrentDictionary<string, string> cache = new();
    private readonly ConcurrentDictionary<string, string> nearCache = new();
    private readonly ILogger<InMemoryCache> logger = logger;

    public bool Contains(string src)
    {
        if (this.cache.ContainsKey(src) || this.nearCache.ContainsKey(src))
        {
            return true;
        }
        if (this.cache.IsEmpty)
        {
            return false;
        }
        var t = DateTime.UtcNow;
        var (cacheSrc, dst, distance) = this.cache
            .Select(p => (src: p.Key, dst: p.Value, distance: Levenshtein.GetDistance(src, p.Key, CalculationOptions.DefaultWithThreading)))
            .MinBy(p => p.distance);
        // 編集距離のパーセンテージ
        var p = 100.0 * distance / Math.Max(src.Length, cacheSrc.Length);
        this.logger.LogDebug($"LevenshteinDistance: {src} -> {cacheSrc} ({p:f2}%) [{DateTime.UtcNow - t}]");
        if (p >= 10)
        {
            return false;
        }
        this.nearCache.TryAdd(src, dst);
        return true;
    }

    public void AddRange(IEnumerable<(string src, string dst)> pairs)
    {
        foreach (var (src, dst) in pairs)
        {
            this.cache.AddOrUpdate(src, dst, (_, _) => dst);
        }
    }
    public string Get(string src)
        => this.cache.TryGetValue(src, out var dst) ? dst
            : this.nearCache.TryGetValue(src, out dst) ? dst
            : string.Empty;
}