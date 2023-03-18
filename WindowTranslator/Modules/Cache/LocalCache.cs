using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Cache;

[DisplayName("ローカルファイルキャッシュ")]
[DefaultModule]
public sealed class LocalCache : ICacheModule, IDisposable
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
    private readonly IProcessInfoStore processInfoStore;
    private readonly ConcurrentDictionary<string, string> cache;
    private readonly string path;

    public LocalCache(IProcessInfoStore processInfoStore)
    {
        this.processInfoStore = processInfoStore;
        var dir = Path.Combine(PathUtility.UserDir, "cache");
        Directory.CreateDirectory(dir);
        this.path = Path.Combine(dir, Path.ChangeExtension(this.processInfoStore.Name, ".json"));
        if (File.Exists(path))
        {
            using var fs = File.OpenRead(path);
            cache = new(JsonSerializer.Deserialize<Dictionary<string, string>>(fs, serializerOptions) ?? new());
        }
        else
        {
            cache = new();
        }
    }

    public void Dispose()
    {
        using var fs = File.OpenWrite(this.path);
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
        => this.cache.ContainsKey(src);

    public string Get(string src)
        => this.cache.TryGetValue(src, out var dst) ? dst : string.Empty;
}
