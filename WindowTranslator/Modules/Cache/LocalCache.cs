using System.Collections.Concurrent;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Kamishibai;
using Microsoft.Extensions.Logging;
using Quickenshtein;
using WindowTranslator.ComponentModel;
using WindowTranslator.Properties;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Cache;

[DefaultModule]
[LocalizedDisplayName(typeof(Resources), nameof(LocalCache))]
public sealed class LocalCache : ICacheModule, IDisposable
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
    private readonly IProcessInfoStore processInfoStore;
    private readonly ConcurrentDictionary<string, string> cache;
    private readonly ConcurrentDictionary<string, string> nearCache = new();
    private readonly ILogger<LocalCache> logger;
    private readonly string path;

    public LocalCache(IProcessInfoStore processInfoStore, IPresentationService presentationService, ILogger<LocalCache> logger)
    {
        this.processInfoStore = processInfoStore;
        this.logger = logger;
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
        if (p >= 20)
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
}
