using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quickenshtein;
using WindowTranslator.ComponentModel;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.Cache;

[LocalizedDisplayName(typeof(Resources), nameof(InMemoryCache))]
public class InMemoryCache(ILogger<InMemoryCache> logger, IOptionsSnapshot<CacheParam> options) : ICacheModule
{
    private readonly ConcurrentDictionary<string, string> cache = new();
    private readonly ConcurrentDictionary<string, string> nearCache = new();
    private readonly ILogger<InMemoryCache> logger = logger;
    private readonly CacheParam options = options.Value;

    public bool Contains(string src)
    {
        if (this.cache.ContainsKey(src) || this.nearCache.ContainsKey(src))
        {
            return true;
        }
        if (this.cache.IsEmpty || Math.Abs(this.options.FazzyMatchThreshold - 1.0) < double.Epsilon)
        {
            return false;
        }
        var t = DateTime.UtcNow;
        var (cacheSrc, dst, distance) = this.cache
            .Select(p => (src: p.Key, dst: p.Value, distance: Levenshtein.GetDistance(src, p.Key, CalculationOptions.DefaultWithThreading)))
            .MinBy(p => p.distance);
        // 編集距離のパーセンテージ
        var p = (float)distance / Math.Max(src.Length, cacheSrc.Length);
        this.logger.LogDebug($"LevenshteinDistance: {src} -> {cacheSrc} ({p:p2}%) [{DateTime.UtcNow - t}]");
        if (p < this.options.FazzyMatchThreshold)
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