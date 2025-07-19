using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quickenshtein;

namespace WindowTranslator.Modules.Cache;

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
        if (this.cache.IsEmpty || Math.Abs(this.options.FuzzyMatchThreshold - 1.0) < double.Epsilon)
        {
            return false;
        }
        var t = DateTime.UtcNow;
        var (cacheSrc, dst, distance) = this.cache
            .Select(p => (src: p.Key, dst: p.Value, distance: Levenshtein.GetDistance(src, p.Key, CalculationOptions.DefaultWithThreading)))
            .MinBy(p => p.distance);

        // 元の文字列がキャッシュより長い場合は、文字アニメで伸びているかもなので、キャッシュ対象外
        if (src.Length > cacheSrc.Length)
        {
            if (src.StartsWith(cacheSrc, StringComparison.OrdinalIgnoreCase))
            {
                // 伸びているテキストなら、短い方をキャッシュから削除する(揺れ防止のため)
                this.cache.TryRemove(cacheSrc, out _);
            }
            return false;
        }

        // 一致率の計算
        var p = 1 - ((float)distance / Math.Max(src.Length, cacheSrc.Length));
        this.logger.LogDebug($"LevenshteinDistance: {src} -> {cacheSrc} ({p:p2}%) [{DateTime.UtcNow - t}]");
        if (p < this.options.FuzzyMatchThreshold)
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