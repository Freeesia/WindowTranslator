using System.Diagnostics;
using WindowTranslator.Modules;
using WindowTranslator.Stores;
using PInvoke;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Quickenshtein;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace WindowTranslator.Plugin.FoMPlugin;

public class FoMFilterModule : IFilterModule
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNameCaseInsensitive = true,
    };
    private readonly bool isTarget;
    private readonly FrozenDictionary<string, string> builtin;
    private readonly ConcurrentDictionary<string, (string en, string ja)> cache = [];
    private readonly ILogger<FoMFilterModule> logger;

    public double Priority => -1;

    public FoMFilterModule(IProcessInfoStore processInfo, IOptions<FoMOptions> options, ILogger<FoMFilterModule> logger)
    {
        _ = User32.GetWindowThreadProcessId(processInfo.MainWindowHandle, out var processId);
        if (GetProcessPath(processId) is { } exePath && Path.GetFileName(exePath) == "FieldsOfMistria.exe")
        {
            this.isTarget = true;
            var path = Path.Combine(Path.GetDirectoryName(exePath)!, "localization.json");
            using var fs = File.OpenRead(path);
            var loc = JsonSerializer.Deserialize<Localization>(fs, serializerOptions);
            var name = options.Value.PlayerName;
            this.builtin = loc!.Eng
                .DistinctBy(p => p.Value)
                .Select(p => (en: p.Value.Replace("[Ari]", name), ja: loc.Jpn.TryGetValue(p.Key, out var s) && s != "MISSING" ? s.Replace("[Ari]", name) : string.Empty))
                .ToFrozenDictionary(p => p.en, p => p.ja);
        }
        else
        {
            this.builtin = FrozenDictionary<string, string>.Empty;
        }
        this.logger = logger;
    }

    public IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts)
    {
        if (this.isTarget)
        {
            return texts.Select(Correct).OfType<TextRect>();
        }
        else
        {
            return texts;
        }
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts)
        => texts;

    public TextRect? Correct(TextRect src)
    {
        if (this.builtin.TryGetValue(src.Text, out var dst))
        {
            return string.IsNullOrEmpty(dst) ? src : src with { Text = dst, IsTranslated = true };
        }
        else if (this.cache.TryGetValue(src.Text, out var c))
        {
            return string.IsNullOrEmpty(c.ja) ? src with { Text = c.en } : src with { Text = c.ja, IsTranslated = true };
        }
        var t = DateTime.UtcNow;
        // TODO: $マーク、改行の調整
        var (key, near, l) = this.builtin.Select(p => (p.Key, p.Value, length: Levenshtein.GetDistance(p.Key, src.Text, CalculationOptions.DefaultWithThreading))).MinBy(s => s.length);
        // 編集距離のパーセンテージ
        var p = 100.0 * l / Math.Max(src.Text.Length, key.Length);
        this.logger.LogDebug($"LevenshteinDistance: {src.Text} -> {key} ({p:f2}%) [{DateTime.UtcNow - t}]");
        // 編集距離が短いほうの30%以下なら利用する
        if (p < 30)
        {
            this.cache.TryAdd(src.Text, (key, near));
            if (string.IsNullOrEmpty(near))
            {
                return src with { Text = key };
            }
            else
            {
                return src with { Text = near, IsTranslated = true };
            }
        }
        return null;
    }

    private static string? GetProcessPath(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var module = process.MainModule;
            return module?.FileName ?? string.Empty;
        }
        catch (Exception)
        {
            // プロセスが終了している場合がある
            return null;
        }
    }
}

public record Localization(Dictionary<string, string> Eng, Dictionary<string, string> Jpn);

public class FoMOptions : IPluginParam
{
    public string PlayerName { get; set; } = string.Empty;
}
