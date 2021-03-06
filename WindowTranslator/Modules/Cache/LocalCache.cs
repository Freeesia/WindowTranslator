using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.Cache;
public sealed class LocalCache : ICacheModule, IDisposable
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
    private readonly IProcessInfoStore processInfoStore;
    private readonly Dictionary<string, string> cache;
    private readonly string path;

    public LocalCache(IProcessInfoStore processInfoStore)
    {
        this.processInfoStore = processInfoStore;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowTranslator");
        Directory.CreateDirectory(dir);
        this.path = Path.Combine(dir, Path.ChangeExtension(Path.GetFileName(this.processInfoStore.FileName), ".json"));
        if (File.Exists(path))
        {
            using var fs = File.OpenRead(path);
            cache = JsonSerializer.Deserialize<Dictionary<string, string>>(fs, serializerOptions) ?? new();
        }
        else
        {
            cache = new();
        }
    }

    public void Dispose()
    {
        using var fs = File.Open(this.path, FileMode.Create, FileAccess.Write);
        JsonSerializer.Serialize(fs, this.cache, serializerOptions);
    }

    public void AddRange(IEnumerable<(string src, string dst)> pairs)
    {
        foreach (var (src, dst) in pairs)
        {
            this.cache.Add(src, dst);
        }
    }

    public bool Contains(string src)
        => this.cache.ContainsKey(src);

    public string Get(string src)
        => this.cache[src];
}
