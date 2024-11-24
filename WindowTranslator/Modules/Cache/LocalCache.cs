using System.Collections.Concurrent;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Kamishibai;
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
    private readonly string path;

    public LocalCache(IProcessInfoStore processInfoStore, IPresentationService presentationService)
    {
        this.processInfoStore = processInfoStore;
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
        => this.cache.ContainsKey(src);

    public string Get(string src)
        => this.cache.TryGetValue(src, out var dst) ? dst : string.Empty;
}
