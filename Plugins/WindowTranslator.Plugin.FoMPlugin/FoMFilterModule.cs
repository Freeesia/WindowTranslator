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
using System.ComponentModel;

namespace WindowTranslator.Plugin.FoMPlugin;

public class FoMFilterModule : IFilterModule
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNameCaseInsensitive = true,
    };
    private readonly bool isEnabled;
    private readonly bool exclude;
    private readonly FrozenDictionary<string, string> builtin;
    private readonly ConcurrentDictionary<string, (string en, string ja)> cache = [];
    private readonly ILogger<FoMFilterModule> logger;

    public double Priority => -1;

    public FoMFilterModule(IProcessInfoStore processInfo, IOptions<FoMOptions> options, ILogger<FoMFilterModule> logger)
    {
        _ = User32.GetWindowThreadProcessId(processInfo.MainWindowHandle, out var processId);
        if (options.Value.IsEnabledCorrect && GetProcessPath(processId) is { } exePath && Path.GetFileName(exePath) == "FieldsOfMistria.exe")
        {
            this.isEnabled = true;
            var path = Path.Combine(Path.GetDirectoryName(exePath)!, "localization.json");
            using var fs = File.OpenRead(path);
            var loc = JsonSerializer.Deserialize<Localization>(fs, serializerOptions);
            var player = options.Value.PlayerName;
            var farm = options.Value.FarmName;
            this.exclude = options.Value.ExcludeUnspecifiedText;
            this.builtin = loc!.Eng
                .Select(p => (
                    en: ReplaceToPlain(p.Value, player, farm).ReplaceLineEndings(string.Empty),
                    ja: loc.Jpn.TryGetValue(p.Key, out var s) && s != "MISSING" ? ReplaceToPlain(s, player, farm) : string.Empty))
                // 置換系は対象外
                .Where(p => !p.en.Contains('['))
                .DistinctBy(p => p.en)
                .ToFrozenDictionary(p => p.en, p => p.ja);
        }
        else
        {
            this.builtin = FrozenDictionary<string, string>.Empty;
        }
        this.logger = logger;
    }

    private static string ReplaceToPlain(string s, string player, string farm)
        => s.Replace("[Ari]", player)
            .Replace("[farm_name]", farm)
            .Replace("$", string.Empty)
            .Replace("=", string.Empty);

    public IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts)
    {
        if (this.isEnabled)
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
        return exclude ? null : src;
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

[DisplayName("Fields of Mistria専用")]
public class FoMOptions : IPluginParam
{
    [DisplayName("ゲームに含まれているリソースを利用した補正を利用する")]
    public bool IsEnabledCorrect { get; set; } = true;

    [DisplayName("プレイヤー名")]
    public string PlayerName { get; set; } = string.Empty;

    [DisplayName("農場名")]
    public string FarmName { get; set; } = string.Empty;

    [DisplayName("特定できないテキストを除外")]
    public bool ExcludeUnspecifiedText { get; set; } = true;
}
