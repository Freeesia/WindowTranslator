using System.Collections.Concurrent;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using Kamishibai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quickenshtein;
using WindowTranslator.ComponentModel;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Cache;

[DefaultModule]
public sealed partial class LocalCache : ICacheModule, IDisposable
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
    private readonly IProcessInfoStore processInfoStore;
    private readonly ConcurrentDictionary<string, string> cache;
    private readonly ConcurrentDictionary<string, string> nearCache = new();
    private readonly ILogger<LocalCache> logger;
    private readonly CacheParam options;
    private readonly string path;

    public LocalCache(IProcessInfoStore processInfoStore, IPresentationService presentationService, ILogger<LocalCache> logger, IOptionsSnapshot<CacheParam> options)
    {
        this.processInfoStore = processInfoStore;
        this.logger = logger;
        this.options = options.Value;
        var dir = Path.Combine(PathUtility.UserDir, "cache");
        Directory.CreateDirectory(dir);
        this.path = Path.Combine(dir, Path.ChangeExtension(this.processInfoStore.Name, ".json"));
        try
        {
            if (File.Exists(path))
            {
                using var fs = File.OpenRead(path);
                cache = new(JsonSerializer.Deserialize<Dictionary<string, string>>(fs, serializerOptions) ?? []);
                return;
            }
        }
        // 読み込めなかったら新規作成
        catch
        {
            presentationService.ShowMessage("キャッシュを読み込めなかったので、新しくキャッシュを作ります", icon: MessageBoxImage.Warning);
        }
        cache = new();
    }

    public void Dispose()
    {
        using var fs = File.Open(this.path, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(fs, this.cache, serializerOptions);
    }

    public void AddRange(IEnumerable<(string src, string dst)> pairs)
    {
        foreach (var (src, dst) in pairs)
        {
            this.cache.AddOrUpdate(src, dst, (_, _) => dst);
        }
    }

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
        // 一致率の計算
        var p = 1 - ((float)distance / Math.Max(src.Length, cacheSrc.Length));
        this.logger.LogDebug($"LevenshteinDistance: {src} -> {cacheSrc} ({p:p2}%) [{DateTime.UtcNow - t}]");
        if (p < this.options.FuzzyMatchThreshold)
        {
            return false;
        }
        // src,cacheSrcの中にある全ての数字が一致しない場合はキャッシュを使わない
        if (!NumberPattern().Matches(src).Select(m => m.Value).SequenceEqual(NumberPattern().Matches(cacheSrc).Select(m => m.Value)))
        {
            return false;
        }

        this.nearCache.TryAdd(src, dst);
        return true;
    }

    public string Get(string src)
        => this.cache.TryGetValue(src, out var dst) ? dst
            : this.nearCache.TryGetValue(src, out dst) ? dst
            : string.Empty;

    [GeneratedRegex(@"[\d,\.]+")]
    private static partial Regex NumberPattern();
}
