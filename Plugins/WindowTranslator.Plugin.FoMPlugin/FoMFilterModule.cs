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
using System.Threading.Channels;
using System.Text.RegularExpressions;

namespace WindowTranslator.Plugin.FoMPlugin;

public partial class FoMFilterModule : IFilterModule
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNameCaseInsensitive = true,
    };
    private readonly bool isEnabled;
    private readonly bool exclude;
    private readonly FrozenDictionary<string, LocInto> builtin;
    private readonly ConcurrentDictionary<string, (string en, string ja)> cache = [];
    private readonly Channel<IReadOnlyList<string>> queue;
    private readonly ILogger<FoMFilterModule> logger;

    public double Priority => -1;

    public FoMFilterModule(IProcessInfoStore processInfo, ITranslateModule translateModule, IOptions<FoMOptions> options, ILogger<FoMFilterModule> logger)
    {
        this.queue = Channel.CreateBounded<IReadOnlyList<string>>(new(1)
        {
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        }, Dropped);
        this.logger = logger;
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
                    en: ReplaceToPlain(p.Value, player, farm),
                    ja: new LocInto(p.Key, loc.Jpn.TryGetValue(p.Key, out var s) && s != "MISSING" ? ReplaceToPlain(s, player, farm) : string.Empty)))
                // OCRで段落ごとに分割されている場合があるので、それを考慮する
                .SelectMany(p => SplitParagraph(p.en, p.ja))
                // OCRでは改行コードが抜けているので、編集距離を計算する際に邪魔になる
                .Select(p => (en: p.en.ReplaceLineEndings(string.Empty), p.ja))
                // 置換系は対象外
                .Where(p => !p.en.Contains('['))
                .DistinctBy(p => p.en)
                .ToFrozenDictionary(p => p.en, p => p.ja);

            // キャラ名やアイテム名を用語集として登録
            translateModule.RegisterGlossaryAsync(
                this.builtin.Where(p => GlossaryRegex().IsMatch(p.Value.Key))
                    .Select(p => (p.Key, p.Value.Text))
                    .Append((player, player))
                    .Append((farm, farm))
                    .Where(p => !string.IsNullOrEmpty(p.Item2))
                    .DistinctBy(p => p.Item1)
                    .ToDictionary(p => p.Item1, p => p.Item2));
            Task.Run(Correct);
        }
        else
        {
            this.builtin = FrozenDictionary<string, LocInto>.Empty;
        }
    }

    private static string ReplaceToPlain(string s, string player, string farm)
        => s.Replace("[Ari]", player)
            .Replace("[farm_name]", farm)
            .Replace("$", string.Empty)
            .Replace("=", string.Empty)
            .Replace("{}", string.Empty);

    private static IEnumerable<(string en, LocInto ja)> SplitParagraph(string en, LocInto ja)
    {
        var enLines = en.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        var jaLines = ja.Text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        if (enLines.Length == jaLines.Length)
        {
            return enLines.Zip(jaLines, (e, j) => (e, ja with { Text = j }));
        }
        else if (enLines.Length > jaLines.Length)
        {
            return enLines.Select((e, i) => (e, ja with { Text = i < jaLines.Length ? jaLines[i] : string.Empty }));
        }
        else
        {
            return enLines.Select((e, i) => (e, ja with { Text = i == enLines.Length - 1 ? string.Join("\n\n", jaLines[i..]) : jaLines[i] }));
        }
    }


    public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts)
    {
        if (!this.isEnabled)
        {
            await foreach (var text in texts.ConfigureAwait(false))
            {
                yield return text;
            }
            yield break;
        }
        var targets = new List<string>();
        await foreach (var src in texts.ConfigureAwait(false))
        {
            if (this.builtin.TryGetValue(src.Text, out var dst))
            {
                yield return string.IsNullOrEmpty(dst.Text) ? src : src with { Text = dst.Text, IsTranslated = true };
            }
            else if (this.cache.TryGetValue(src.Text, out var c))
            {
                yield return string.IsNullOrEmpty(c.ja) ? src with { Text = c.en } : src with { Text = c.ja, IsTranslated = true };
            }
            else
            {
                targets.Add(src.Text);
                if (!this.exclude)
                {
                    yield return src;
                }
            }
        }

        if (targets.Count > 0)
        {
            await this.queue.Writer.WriteAsync(targets).ConfigureAwait(false);
        }
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts)
        => texts;

    private void Dropped(IReadOnlyList<string> texts)
        => this.logger.LogDebug($"Dropped texts: {string.Join(", ", texts)}");

    private async Task Correct()
    {
        await foreach (var texts in this.queue.Reader.ReadAllAsync())
        {
            foreach (var text in texts)
            {
                var t = DateTime.UtcNow;
                var (key, near, l) = this.builtin.Select(p => (p.Key, p.Value.Text, length: Levenshtein.GetDistance(p.Key, text, CalculationOptions.DefaultWithThreading))).MinBy(s => s.length);
                // 編集距離のパーセンテージ
                var p = 100.0 * l / Math.Max(text.Length, key.Length);
                this.logger.LogDebug($"LevenshteinDistance: {text} -> {key} ({p:f2}%) [{DateTime.UtcNow - t}]");
                // 編集距離が短いほうの30%以下なら利用する
                if (p < 30)
                {
                    this.cache.TryAdd(text, (key, near));
                }
            }
        }
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

    [GeneratedRegex("^(npcs|items|locations|festivals)/.*/name$")]
    private static partial Regex GlossaryRegex();
}

record Localization(Dictionary<string, string> Eng, Dictionary<string, string> Jpn);
record LocInto(string Key, string Text);


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
