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
using PropertyTools.DataAnnotations;
using System.Text.Json.Serialization;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

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
    private readonly ConcurrentDictionary<string, (string en, string ja, string context)> cache = [];
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
            translateModule.RegisterContext("""
                This is a nostalgic farming / life sim RPG game like Harvest Moon.
                It is set in rural medieval Europe.
                """);
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
            .Replace("^", string.Empty)
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
        var match = new List<string>();
        var notContexts = new List<TextRect>();
        var targets = new List<string>();
        await foreach (var src in texts.ConfigureAwait(false))
        {
            if (this.builtin.TryGetValue(src.Text, out var dst))
            {
                match.Add(src.Text);
                var ret = string.IsNullOrEmpty(dst.Text) ? src with { Context = GetContext(dst.Key) } : src with { Text = dst.Text, IsTranslated = true };
                if (ret.IsTranslated || !string.IsNullOrEmpty(ret.Context))
                {
                    yield return ret;
                }
                else
                {
                    notContexts.Add(ret);
                }
            }
            else if (this.cache.TryGetValue(src.Text, out var c))
            {
                match.Add(c.en);
                var ret = string.IsNullOrEmpty(c.ja) ? src with { Text = c.en, Context = c.context } : src with { Text = c.ja, IsTranslated = true };
                if (ret.IsTranslated || !string.IsNullOrEmpty(ret.Context))
                {
                    yield return ret;
                }
                else
                {
                    notContexts.Add(ret);
                }
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
        if (notContexts.Count > 0)
        {
            var contexts = match.Select(GetChatContext).Distinct().Where(c => !string.IsNullOrEmpty(c)).ToArray();
            if (contexts is [var context])
            {
                notContexts = notContexts.Select(r => r with { Context = context }).ToList();
            }
            foreach (var item in notContexts)
            {
                yield return item;
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
                var (key, en, near, l) = this.builtin.Select(p => (p.Value.Key, p.Key, p.Value.Text, length: Levenshtein.GetDistance(p.Key, text, CalculationOptions.DefaultWithThreading))).MinBy(s => s.length);
                // 編集距離のパーセンテージ
                var p = 100.0 * l / Math.Max(text.Length, en.Length);
                this.logger.LogDebug($"LevenshteinDistance: {text} -> {en} ({p:f2}%) [{DateTime.UtcNow - t}]");
                // 編集距離が短いほうの30%以下なら利用する
                if (p >= 30)
                {
                    continue;
                }
                this.cache.TryAdd(text, (en, near, GetContext(key)));
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

    private static string GetContext(string key)
        => key.Split('/') is ["Conversations" or "Cutscenes", _, var c, ..] ? GetChatContext(c) : string.Empty;

    private static string GetChatContext(string character)
        => character switch
        {
            "Celine" => """
                This line is said by the female character.
                She is the daughter of Holt and Nora, co-owners of The General Store, and older sister to Dell.
                She has a sweet, caring, and friendly personality, as well as an affinity for all things flora.
                Her first person is "私".
                """,
            "Juniper" => """
                This line is said by the female character.
                She is a mysterious sorceress drawn to Mistria when she learns that magical mists and monsters of legend have begun appearing in the area after the recent earthquake.
                Her first person is "わたくし".
                """,
            "Reina" => """
                This line is said by the female character.
                She works at the Sleeping Dragon Inn and enjoys reading in front of the fireplace on a rainy day.
                She is the daughter of Hemlock and Josephine and the older sister of Luc and Maple.
                She is hard-working, friendly, and a romantic. She loves cooking, often experimenting with new recipes.
                Her first person is "ワタシ".
                """,
            "Valen" => """
                This line is said by the female character.
                She works in Mistria Clinic, which has been owned by her family for generations.
                Valen displays an informal, friendly approach to her profession as she insists that the player call her Valen instead of Doctor.
                Her first person is "私".
                """,
            "Adeline" => """
                This line is said by the female character.
                She is the daughter of the Baron and Baroness of Mistria and its current Town Leader.
                The "restoration and revitalization" of Mistria is her life's goal and she works tirelessly to this end.
                Luckily for her, Adeline's work often doubles as her play--things like filling out tax documents are fun to her--and she takes on each task with equal parts determination and enthusiasm.
                Her first person is "私".
                """,
            "Balor" => """
                This line is said by the male character.
                He is a merchant.
                His first person is "俺".
                """,
            "March" => """
                This line is said by the male character.
                He is an award-winning blacksmith with a chip on his shoulder.
                While he'll be curt with you at first, and is a bit vain and grumpy, his heart is not impenetrable.
                His first person is "オレ".
                """,
            "Hayden" => """
                This line is said by the male character.
                He is Bold and friendly.
                He loves animals, especially his prize chicken Henrietta.
                His first person is "オレ".
                """,
            "Ryis" => """
                This line is said by the male character.
                Like his Uncle Landen, he is a woodworker and runs the Carpenter's Shop in The Eastern Road.
                He has a thoughtful and calm personality.
                His first person is "僕".
                """,
            "Eiland" => """
                This line is said by the male character.
                He likes archaeology, and is head of the Historical Society.
                He believes that reconnecting with Mistria's past is the key to its future.
                His first person is "僕".
                """,
            "Dell" => """
                This line is said by the little girl.
                She is the excitable, wildchild.
                Her first person is "ボク".
                """,
            "Dozy" => """
                This line is the dog's feeling.
                Juniper's trusty and reliable familiar, without whom the bathhouse would surely fall apart. A very good boy.
                """,
            "Elsie" => """
                This line is said by the woman of odd age.
                Her first person is "私".
                """,
            "Errol" => """
                This line is said by the man of odd age.
                He is a member of the Historical Society, and works as the curator at Mistria's museum.
                He also used to be the previous mines foreman.
                His first person is "ワシ".
                """,
            "Hemlock" => """
                This line is said by the man of odd age.
                Laid-back innkeeper, former touring musician.
                Married to Josephine, father to Reina, Luc and Maple.
                His first person is "ボク".
                """,
            "Holt" => """
                This line is said by the man of odd age.
                He is Nora's husband, as well as Celine and Dell's father. He has a penchant for puns.
                His first person is "僕".
                """,
            "Henrietta" => """
                This line is the chicken's feeling.
                Hayden's prize pet chicken.
                """,
            "Josephine" => """
                This line is said by the woman of odd age.
                She is Hemlock's wife, as well as Reina, Luc and Maple's mother.
                Her first person is "アタシ".
                """,
            "Landen" => """
                This line is said by the man of odd age.
                Suave, 'retired' carpenter who, ah, you're doing it wrong, let me show you how a pro does it! Uncle to Ryis.
                His first person is "ボク".
                """,
            "Luc" => """
                This line is said by the little boy.
                He is a budding entomologist with big dreams and bigger bugs.
                His first person is "ぼく".
                """,
            "Maple" => """
                This line is said by the little girl.
                Her first person is "わたし".
                """,
            "Nora" => """
                This line is said by the woman of odd age.
                As both the Head of Commerce and the Saturday Market for Mistria, Nora is a rather keen, business-minded woman who keeps all her ledgers neat and tidy.
                While she can be stern--at least in comparison to her husband--Nora isn't too stuffy to enjoy a game.
                Her first person is "ワタシ".
                """,
            "Olric" => """
                This line is said by the male character.
                He has a friendly and cheerful personality, and lives at the Blacksmith with his little brother March.
                His first person is "オレ".
                """,
            "Terithia" => """
                This line is said by the woman of odd age.
                She is rough and tough fisherwoman and former soldier with a million stories to tell, living by the ocean.
                Her first person is "ワシ".
                """,
            "Darcy" => """
                This line is said by the female character.
                She is a soft-spoken lady.
                """,
            "Louis" => """
                This line is said by the man of odd age.
                He was once a Legendary tailor, however he was banished from the Capital.
                His first person is "私".
                """,
            "Merri" => """
                This line is said by the female character.
                She is a plump woman.
                Her first person is "私".
                """,
            "Vera" => """
                This line is said by the female character.
                She is a friendly lady.
                Her first person is "私".
                """,
            "Caldarus" => """
                This line is said by a male dragon.
                He speaks with a pompous and dignified tone.
                """,
            "Priestess" => """
                This line is a strange woman's statement.
                She speaks in one language.
                Her first person is "私".
                """,
            _ => string.Empty,
        };
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

    [JsonIgnore]
    [Comment]
    public string Comment { get; } = """
        各キャラクターの文脈情報は以下のWikiを参考に指定しています。
        https://fieldsofmistria.wiki.gg/wiki/Characters
        Page content is under the Creative Commons Attribution-ShareAlike 4.0 License unless otherwise noted.
        """;
}
