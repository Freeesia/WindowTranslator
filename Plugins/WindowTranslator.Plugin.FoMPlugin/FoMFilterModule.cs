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
using System.Threading.Channels;
using System.Text.RegularExpressions;
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
    private readonly bool useJpn;
    private readonly bool exclude;
    private readonly FrozenDictionary<string, LocInto> builtin;
    private readonly FrozenDictionary<string, string> scenes;
    private readonly FrozenDictionary<string, string> context;
    private readonly ConcurrentDictionary<string, CacheInfo> cache = [];
    private readonly Channel<IReadOnlyList<string>> queue;
    private readonly ILogger<FoMFilterModule> logger;
    private static readonly Dictionary<string, string> charContext = new()
    {
        ["Celine"] = """
                この文章はCelineという女性のセリフです。
                彼女の父親はHoltで、母親がNoraで、妹にDellがいます。
                彼女はとても面倒見がよく、人懐っこい性格です。
                誰に対しても丁寧な口調で話しますが、家族に対しては少しフランクな口調になります。
                彼女の一人称は漢字で「私」です。
                """,
        ["Juniper"] = """
                この文章はJuniperという女性のセリフです。
                ミステリアスな魔術師で、高慢な性格です。
                気品漂う振る舞いとともに、他人を見下すような発言をしつつもどこか憎めない存在感を放っています。
                90年代アニメのお嬢様のような口調です。
                彼女の一人称はカタカナで「アタシ」です。
                """,
        ["Reina"] = """
                この文章はReinaという女性のセリフです。
                彼女は「the Sleeping Dragon Inn」で働き、雨の日は暖炉の前で読書を楽しみます。
                彼女の父親はHemlockで、母親がJosephineで、妹にMaple、弟にLucがいます。
                料理が好きで、よく新しいレシピを試しています。
                ロマンチストですが、気さくな性格で誰に対してもフランクな口調で話します。
                彼女の一人称は漢字で「私」です。
                """,
        ["Valen"] = """
                この文章はValenという女性のセリフです。
                彼女はMistria Clinicで働く女医です。
                基本的には冷静沈着な性格ですが、プレイヤーに「ドクター」ではなく、「ヴァレン」と名前で呼ばせるなど気さくでフレンドリーな一面があります。
                彼女の一人称は漢字で「私」です。
                """,
        ["Adeline"] = """
                この文章はAdelineという女性のセリフです。
                彼女は村の男爵夫妻の娘で、現在のタウンリーダーです。
                彼女はとても仲の良い兄のEilandや叔母のElsieと一緒に暮らしています。
                Eilandのことを「エイラント」と名前で呼び、フランクな口調で話します。
                Elisieのことを「おばさま」と呼んでいます。
                村の「復興と活性化」が彼女の人生の目標であり、そのために精力的に働いています。
                幸いなことに、Adelineの仕事は遊びを兼ねていることが多く、税務書類の記入なども楽しんでいます。
                Eiland以外以外には誰に対しても丁寧な口調で話します。
                彼女の一人称は漢字で「私」です。
                """,
        ["Balor"] = """
                この文章はBalorという男性のセリフです。
                彼は商人で、飄々とした性格をしています。
                誰に対しても馴れ馴れしい口調で話しますが、親密になりすぎることはなく、常に一歩引いた関係性を築いています。
                彼の一人称はカタカナで「オレ」です。
                """,
        ["March"] = """
                この文章はMarchという男性のセリフです。
                彼は受賞歴のある鍛冶屋で、肩に傷を抱えています。
                Olricの弟で、Olricのことを「兄貴」と呼んでいます。
                最初は不愛想で少し見栄っ張りで不機嫌ですが、性根は頑固ではありません。いわゆるツンデレな性格です。
                また、酔うととてもテンションが高くなり、フランクな口調になります。
                彼の一人称はカタカナで「オレ」です。
                """,
        ["Hayden"] = """
                この文章はHaydenという男性のセリフです。
                彼は大胆で友好的な性格で、プレイヤーの隣の農場の主です。
                動物が大好きで、特に飼っているニワトリのHenriettaがお気に入りです。
                彼の一人称はカタカナで「オレ」です。
                """,
        ["Ryis"] = """
                この文章はRyisという男性のセリフです。
                彼は木工職人で、The Eastern Roadで大工店を叔父のLandenとともに営んでいます。
                思慮府深く穏やかな性格で、落ち着いた口調で話します。
                彼の一人称はカタカナで「オレ」です。
                """,
        ["Eiland"] = """
                この文章はEilandという男性のセリフです。
                彼は村の男爵夫妻の息子で、考古学が好きで、歴史協会の代表を務めています。
                彼はとても仲の良い妹のAdelineや叔母のElsieと一緒に暮らしています。
                Adelineことを「アデライン」と名前で呼びます。
                Elisieのことを「おばさん」と呼んでいます。
                基本的にはフランクな口調で話します。
                彼の一人称はカタカナで「ボク」です。
                """,
        ["Dell"] = """
                この文章はDellという女の子のセリフです。
                彼女はCelineの妹で、Celineのことを「おねーちゃん」と呼んでいます。
                彼女は男の子勝りのやんちゃな性格です。
                LucやMapleと話すときはリーダーシップをとります。
                彼女の一人称はカタカナで「アタシ」です。
                """,
        ["Dozy"] = """
                この文章はDozyという犬の気持ちです。
                彼はとても賢い犬です。
                文章は全て()で括られます。
                """,
        ["Elsie"] = """
                この文章はElsieという妙齢の女性のセリフです。
                彼女はとてもロマンチストで、よく昔の恋愛を懐かしんでいます。
                物腰の柔らかいお嬢様のような口調です。
                彼女の一人称は漢字で「私」です。
                """,
        ["Errol"] = """
                この文章はErrolという妙齢の男性のセリフです。
                元は鉱員長で、現在は博物館で学芸員をしています。
                豪胆な性格や口調で話します。
                彼の一人称は漢字で「私」です。
                """,
        ["Hemlock"] = """
                この文章はHemlockという妙齢の男性のセリフです。
                彼はのんびりした宿屋の主人で元ツアーミュージシャンです。
                Reina、Luc、Mapleの父で妻はJosephineです。
                彼の一人称はカタカナで「ボク」です。
                """,
        ["Holt"] = """
                この文章はHoltという妙齢の男性のセリフです。
                彼はNoraの夫であり、CelineとDellの父親です。
                とてもダジャレ好きで、誰にでもダジャレを披露します。
                彼の一人称は漢字で「私」です。
                """,
        ["Henrietta"] = """
                この文章はHenriettaというニワトリの気持ちです。
                彼女はとても賢いニワトリで、Haydenのペットです。
                文章は全て()で括られます。
                """,
        ["Josephine"] = """
                この文章はJosephineという妙齢の女性のセリフです。
                彼女はHemlockの妻であり、Reina、Luc、Mapleの母親でもあります。
                彼女の一人称はカタカナで「アタシ」です。
                """,
        ["Landen"] = """
                この文章はLandenという妙齢の男性のセリフです。
                大工を「引退」した風流人で、Ryisの叔父です。
                彼の一人称はカタカナで「ボク」です。
                """,
        ["Luc"] = """
                この文章はLucという男の子のセリフです。
                彼はReineの弟です。Reineのことを「おねえちゃん」と呼んでいます。
                おとなしい性格でDellにリーダーシップを任せています。
                彼は昆虫が好きです。
                彼の一人称はカタカナで「ボク」です。
                """,
        ["Maple"] = """
                この文章はMapleという女の子のセリフです。
                彼女はReineの妹です。Reineのことを「お姉さま」と呼んでいます。
                彼女はお姫様に憧れており、丁寧な口調で話します。
                彼女の一人称はカタカナで「ワタシ」です。
                """,
        ["Nora"] = """
                この文章はNoraという妙齢の女性のセリフです。
                彼女はHoltの妻であり、Celine、Dellの母親でもあります。
                村の商業部長であり、土曜市の責任者でもあるNoraは、かなり鋭敏で、すべての帳簿をきちんと整理整頓しているビジネスマインドの女性です。
                厳しい一面もありますが、ゲームを楽しむ際は堅苦しくなくフレンドリーです。
                彼女の一人称は漢字で「私」です。
                """,
        ["Olric"] = """
                この文章はOlricという男性のセリフです。
                彼は村の鍛冶屋で、Marchの兄です。
                Marchのことを名前で「マルク」と呼んでいます。
                彼はとてもおおらかで、気さくで明るい性格ですが、天然ボケが多いです。
                彼の一人称はカタカナで「ボク」です。
                """,
        ["Terithia"] = """
                この文章はTerithiaという妙齢の女性のセリフです。
                彼女は元軍人の女性で、現在は海辺で暮らす漁師です。
                男勝りで豪胆な性格で口調にもそれが表れています。
                彼女の一人称はカタカナで「アタシ」です。
                """,
        ["Darcy"] = """
                この文章はDarcyという女性のセリフです。
                彼女は物腰の柔らかい女性で、コーヒーショップで働いています。
                彼女の一人称は漢字で「私」です。
                """,
        ["Louis"] = """
                この文章はLouisという妙齢の男性のセリフです。
                かつては王都の仕立て屋でしたが、都から追放されました。
                しかし、彼の聡明な性格は変わりません。
                彼の一人称は漢字で「私」です。
                """,
        ["Merri"] = """
                この文章はMerriという女性のセリフです。
                彼女はふくよかな女性で、家具屋を営んでいます。
                古い家具をリメイクするのが趣味で、新しい家具を作ることも好きです。
                彼女の一人称は漢字で「私」です。
                """,
        ["Vera"] = """
                この文章はVeraという女性のセリフです。
                彼女は気さくでフランクな性格で、美容師をしています。
                彼女の一人称はカタカナで「アタシ」です。
                """,
        ["Caldarus"] = """
                この文章はCaldarusというドラゴンのセリフです。
                彼は古くから守護者として村を見守っています。
                少し古くさい言葉遣いで、尊大で威厳のある男性的な口調で話します。
                彼の一人称は漢字で「私」です。
                """,
        ["Priestess"] = """
                この文章はPriestessという不思議な女性のセリフです。
                彼女はカタコトな言葉で話し、神秘的な雰囲気を持っています。
                彼女の一人称は漢字で「私」です。
                """,
        ["Ari"] = """
                この文章は主人公が会話相手の質問に対して返答するセリフです。
                主人公はプレイヤーが操作するキャラクターです。
                性別はプレイヤーが選択できるので、中性的な言葉遣いをします。
                """,
    };

    public double Priority => -1;

    public FoMFilterModule(IProcessInfoStore processInfo, ITranslateModule translateModule, IOptionsSnapshot<FoMOptions> options, ILogger<FoMFilterModule> logger)
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
            this.useJpn = options.Value.UseJpn;
            var path = Path.Combine(Path.GetDirectoryName(exePath)!, "localization.json");
            using var fs = File.OpenRead(path);
            var loc = JsonSerializer.Deserialize<Localization>(fs, serializerOptions);
            var player = options.Value.PlayerName;
            var farm = options.Value.FarmName;
            this.exclude = options.Value.ExcludeUnspecifiedText;
            this.builtin = loc!.Eng
                .Select(p => (
                    en: p.Value.ReplaceToPlain(player, farm),
                    ja: new LocInto(p.Key, loc.Jpn.TryGetValue(p.Key, out var s) ? s.CorrenctJpn().ReplaceToPlain(player, farm) : string.Empty)))
                // OCRで段落ごとに分割されている場合があるので、それを考慮する
                .SelectMany(p => SplitParagraph(p.en, p.ja))
                // OCRでは改行コードが抜けているので、編集距離を計算する際に邪魔になる
                .Select(p => (en: p.en.ReplaceLineEndings(string.Empty), p.ja))
                // 置換系は対象外
                .Where(p => !p.en.Contains('['))
                .DistinctBy(p => p.en)
                .ToFrozenDictionary(p => p.en, p => p.ja);

            // 会話文全体を抜き出しておく
            static double ParseOrder(string n) => n switch
            {
                "init" => -2,
                "init$_sequence_entry_1$" => -1,
                _ => int.TryParse(n, out var i) ? i : 99,
            };
            this.scenes = loc.Eng
                .Select(p => (key: p.Key.Split('/'), p.Value))
                .Where(p => p.key[0] is "Conversations" or "Cutscenes" or "letters")
                .GroupBy(
                    p => p.key switch
                        {
                        ["Conversations", .., "prompts", _] => string.Join('/', p.key[..^3]),
                        ["Conversations", .., not "prompts", _] => string.Join('/', p.key[..^1]),
                        ["Cutscenes", .., "prompts", _] => string.Join('/', p.key[..^3]),
                        ["Cutscenes", .., not "prompts", _] => string.Join('/', p.key[..^1]),
                        ["letters", ..] => string.Join('/', p.key[..^1]),
                            _ => throw new InvalidOperationException(),
                        },
                    (group, items) => (group, value: items
                        .OrderBy(p => p.key switch
                        {
                        ["Conversations", .., var n, "prompts", var m] => ParseOrder(n) + ((double.Parse(m) + 1) * 0.1),
                        ["Conversations", .., not "prompts", var n] => ParseOrder(n),
                        ["Cutscenes", .., var n, "prompts", var m] => ParseOrder(n) + ((double.Parse(m) + 1) * 0.1),
                        ["Cutscenes", .., not "prompts", var n] => ParseOrder(n),
                        ["letters", _, var key] => key is "local" ? 1.0 : 0.0,
                            _ => throw new InvalidOperationException(),
                        })
                        .Join(group)))
                .Where(p => p.value.Lines() > 1)
                .ToFrozenDictionary(
                    p => p.group,
                    p => $"""

                    
                    以下は翻訳対象の文章が含まれるシーン全体の会話です。
                    シーン全体内の翻訳対象の文章の前後の会話を考慮して、翻訳する際の文脈として利用してください。
                    <シーン全体>
                    {p.value.ReplaceToPlain(player, farm)}
                    </シーン全体>
                    """);

            var sample = loc.Jpn
                .Where(p => p.Key.StartsWith("Conversations/Bank/", StringComparison.Ordinal) && p.Value != "MISSING")
                .Select(p => (p.Key,
                    Ja: p.Value.ReplaceToPlain(player, farm).ReplaceLineEndings(string.Empty),
                    En: loc.Eng[p.Key].ReplaceToPlain(player, farm).ReplaceLineEndings(string.Empty)))
                .GroupBy(p => p.Key.Split('/')[2], t => (t.Ja, t.En))
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(Environment.NewLine + Environment.NewLine, g.Take(5).Select(p => $"英語: {p.En}{Environment.NewLine}日本語: {p.Ja}")));

            this.context = charContext
                .ToFrozenDictionary(
                    p => p.Key,
                    p => sample.TryGetValue(p.Key, out var s) ?
                        $"""
                        {p.Value}

                        以下のテキストはこのキャラクターのセリフの翻訳例です。
                        {s}
                        """ : p.Value);

            // キャラ名やアイテム名を用語集として登録
            translateModule.RegisterGlossaryAsync(
                this.builtin.Where(p => Glossary1Regex().IsMatch(p.Value.Key) || Glossary2Regex().IsMatch(p.Value.Key))
                    .Select(p => (p.Key, p.Value.Text))
                    .Append((player, player))
                    .Append((farm, farm))
                    .Where(p => !string.IsNullOrEmpty(p.Item2))
                    .DistinctBy(p => p.Item1)
                    .ToDictionary(p => p.Item1, p => p.Item2));
            translateModule.RegisterContext("""
                牧場物語のようなノスタルジックな農場シミュレーションRPGです。
                魔法が存在する中世ヨーロッパ風の世界観です。

                地震により混乱が生じ人口が減ってしまったミストリアという村に、プレイヤーは新しく移り住むことになります。
                プレイヤーは農場を経営し、村の人々と交流を深めながら、ミストリアの復興を目指します。
                """);
            Task.Run(Correct);
        }
        else
        {
            this.builtin = FrozenDictionary<string, LocInto>.Empty;
            this.scenes = FrozenDictionary<string, string>.Empty;
            this.context = FrozenDictionary<string, string>.Empty;
        }
    }

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
        var notContexts = new List<(TextRect text, CacheInfo cache)>();
        var targets = new List<string>();
        await foreach (var src in texts.ConfigureAwait(false))
        {
            if (this.builtin.TryGetValue(src.Text, out var dst))
            {
                match.Add(src.Text);
                var keys = dst.Key.Split('/');
                if (this.useJpn && !string.IsNullOrEmpty(dst.Text))
                {
                    yield return src with { Text = dst.Text, IsTranslated = true };
                }
                else if (GetCharContext(keys) is { Length: > 0 } charContext)
                {
                    yield return src with { Context = charContext + GetSceneContext(keys) };
                }
                else if (keys is [.., "prompts", _])
                {
                    yield return src with { Context = GetCharContext("Ari") + GetSceneContext(keys) };
                }
                else
                {
                    notContexts.Add((src, new(keys, src.Text, dst.Text, string.Empty, GetSceneContext(keys))));
                }
            }
            else if (this.cache.TryGetValue(src.Text, out var c))
            {
                match.Add(c.En);
                if (this.useJpn && !string.IsNullOrEmpty(c.Ja))
                {
                    yield return src with { Text = c.Ja, IsTranslated = true };
                }
                else if (!string.IsNullOrEmpty(c.CharContext))
                {
                    yield return src with { Text = c.En, Context = c.CharContext + c.SceneContext };
                }
                else if (c.Keys is [.., "prompts", _])
                {
                    yield return src with { Text = c.En, Context = GetCharContext("Ari") + c.SceneContext };
                }
                else
                {
                    notContexts.Add((src, c));
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
            var contexts = match.Select(GetCharContext).Distinct().Where(c => !string.IsNullOrEmpty(c)).ToArray();
            if (contexts is [var context])
            {
                notContexts = notContexts.Select(p => p with { text = p.text with { Text = p.cache.En, Context = context + p.cache.SceneContext } }).ToList();
            }
            foreach (var (text, _) in notContexts)
            {
                yield return text;
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
                var (key, en, ja, l) = this.builtin.Select(p => (p.Value.Key, p.Key, p.Value.Text, length: Levenshtein.GetDistance(p.Key, text, CalculationOptions.DefaultWithThreading))).MinBy(s => s.length);
                // 編集距離のパーセンテージ
                var p = 100.0 * l / Math.Max(text.Length, en.Length);
                this.logger.LogDebug($"LevenshteinDistance: {text} -> {en} ({p:f2}%) [{DateTime.UtcNow - t}]");
                // 編集距離が短いほうの30%以下なら利用する
                if (p >= 32)
                {
                    continue;
                }
                var keys = key.Split('/');
                this.cache.TryAdd(text, new(keys, en, ja, GetCharContext(keys), GetSceneContext(keys)));
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
    private static partial Regex Glossary1Regex();

    [GeneratedRegex("^misc_local/.*_name$")]
    private static partial Regex Glossary2Regex();

    private string GetSceneContext(string[] keys)
    {
        var scene = keys switch
        {
        ["Conversations", .., "prompts", _] => string.Join('/', keys[..^3]),
        ["Conversations", .., not "prompts", _] => string.Join('/', keys[..^1]),
        ["Cutscenes", .., "prompts", _] => string.Join('/', keys[..^3]),
        ["Cutscenes", .., not "prompts", _] => string.Join('/', keys[..^1]),
        ["letters", ..] => string.Join('/', keys[..^1]),
            _ => string.Empty,
        };
        return this.scenes.TryGetValue(scene, out var s) ? s : string.Empty;
    }

    private string GetCharContext(string charName)
        => this.context.TryGetValue(charName, out var context) ? context : string.Empty;

    private string GetCharContext(string[] keys)
        => keys is ["Conversations" or "Cutscenes", _, var c, ..] ? GetCharContext(c) : string.Empty;
}

record Localization(Dictionary<string, string> Eng, Dictionary<string, string> Jpn);
record LocInto(string Key, string Text);

record CacheInfo(string[] Keys, string En, string Ja, string CharContext, string SceneContext);

[DisplayName("Fields of Mistria専用")]
public class FoMOptions : IPluginParam
{
    [DisplayName("ゲームに含まれているリソースを利用した補正を利用する")]
    public bool IsEnabledCorrect { get; set; } = true;

    [DisplayName("ゲームに含まれている日本語リソースを利用する")]
    public bool UseJpn { get; set; } = true;

    [DisplayName("プレイヤー名")]
    public string PlayerName { get; set; } = string.Empty;

    [DisplayName("農場名")]
    public string FarmName { get; set; } = string.Empty;

    [DisplayName("特定できないテキストを除外")]
    public bool ExcludeUnspecifiedText { get; set; } = true;
}

file static class Extentions
{

    public static string CorrenctJpn(this string s)
        => s switch
        {
            "MISSING" => string.Empty,
            "近い" => "閉じる",
            "出口" => "終了",
            _ => s,
        };

    public static string ReplaceToPlain(this string s, string player, string farm)
        => s.Replace("[Ari]", player)
            .Replace("[farm_name]", farm)
            .Replace("[ANIMAL_NAME]", "it")
            .Replace("[ANIMAL_PAIR]", "the other one")
            .Replace("[BREEDING_PARTNER]", string.Empty)
            .Replace("[TREAT_ITEM]", "Treat")
            .Replace("[BATHHOUSE_COST]", string.Empty)
            .Replace("[INN_SOUP_OF_THE_DAY]", string.Empty)
            .Replace("[pet_name]", string.Empty)
            .Replace("[pet_type]", string.Empty)
            .Replace("[festival_large_animal_name]", string.Empty)
            .Replace("[festival_large_animal_type]", string.Empty)
            .Replace("[festival_small_animal_name]", string.Empty)
            .Replace("[festival_small_animal_type]", string.Empty)
            //.Replace("[statue_cost_low]", string.Empty)
            //.Replace("[statue_cost_high]", string.Empty)
            //.Replace("[offering_item_count]", string.Empty)
            //.Replace("[offering_item_name]", string.Empty)
            .Replace("$", string.Empty)
            .Replace("=", string.Empty)
            .Replace("^", string.Empty)
            .Replace("{}", string.Empty);

    public static string Join(this IEnumerable<(string[] key, string Value)> values, string group)
        => group.Split('/')[0] switch
        {
            "Conversations" => string.Join(Environment.NewLine, values.Select(p => p.key[^2] is "prompts" ? $"選択肢 {p.key[^1]} : \"{p.Value}\"" : $"\"{p.Value}\"")),
            "Cutscenes" => string.Join(Environment.NewLine, values.Select(p => p.key[^2] is "prompts" ? $"選択肢 {p.key[^1]} : \"{p.Value}\"" : $"\"{p.Value}\"")),
            "letters" => string.Join(Environment.NewLine, values.Select(p => (p.key[^1] is "local" ? "本文: \r\n" : "件名: ") + p.Value)),
            _ => throw new InvalidOperationException(),
        };

    public static int Lines(this string s)
    {
        var count = 0;
        var span = s.AsSpan();
        foreach (var _ in span.EnumerateLines())
        {
            count++;
        }
        return count;
    }
}