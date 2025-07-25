﻿using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.LLMPlugin;

public class LLMTranslator : ITranslateModule
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true,
    };
    private static readonly ChatCompletionOptions openAiOptions = new()
    {
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            "translated_array",
            BinaryData.FromBytes("""
                {
                    "type": "object",
                    "properties": {
                        "translated": {
                            "type": "array",
                            "items": { "type": "string" }
                        }
                    },
                    "required": ["translated"],
                    "additionalProperties": false
                }
                """u8.ToArray()),
            "翻訳後のテキストの配列",
            true),
    };
    private static readonly ChatCompletionOptions otherOptions = new()
    {
        StopSequences = { "\"]}" },
    };
    private readonly string preSystem;
    private readonly string? userContext;
    private readonly string postSystem;
    private readonly ChatClient? client;
    private readonly bool isOpenAi;
    private readonly IDictionary<string, string> glossary = new Dictionary<string, string>();
    private IReadOnlyList<string> common = [];
    private string? context;

    private static readonly ChatMessage assitant = ChatMessage.CreateAssistantMessage("{\"translated\": [\"");

    public LLMTranslator(IOptionsSnapshot<LLMOptions> llmOptions, IOptionsSnapshot<LanguageOptions> langOptions)
    {
        var srcLang = CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName;
        var targetLang = CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName;

        this.preSystem = $$"""
        あなたは{{srcLang}}から{{targetLang}}へ翻訳するの専門家です。
        入力テキストは{{srcLang}}のテキストであり、翻訳が必要です。
        渡されたテキストを{{targetLang}}へ翻訳して出力してください。
        """;
        this.userContext = llmOptions.Value.TranslateContext;
        this.postSystem = """
        入力テキストは以下のJsonフォーマットになっています。
        各textの内容はペアとなるcontextの文脈を考慮して翻訳してください。
        contextに一人称が指定されている場合は、漢字、ひらがな、カタカナの表記を変更せずに一人称をそのまま使ってください。
        翻訳対象のテキストが判別できない場合は、翻訳を行わずにそのままの表記を利用してください。
        <入力テキストのJsonフォーマット>
        [{"text":"翻訳対象のテキスト1", "context": "翻訳対象のテキスト1の文脈"}, {"text":"翻訳対象のテキスト2", "context": "翻訳対象のテキスト2の文脈"}]
        </入力テキストのJsonフォーマット>
        
        出力は以下の文字列型の配列を持ったJsonフォーマットです。
        入力されたテキストの順序を維持して翻訳したテキストを出力してください。
        <出力テキストのJsonフォーマット>
        {"translated": ["翻訳したテキスト1", "翻訳したテキスト2"]}
        </出力テキストのJsonフォーマット>
        """;

        if (llmOptions.Value.Model is { Length: > 0 } model && llmOptions.Value.ApiKey is { Length: > 0 } apiKey)
        {
            if (llmOptions.Value.Endpoint is { Length: > 0 } e)
            {
                this.client = new(model, new(apiKey), new OpenAIClientOptions() { Endpoint = new(e) });
            }
            else
            {
                this.isOpenAi = true;
                this.client = new(model, apiKey);
            }
        }

        if (File.Exists(llmOptions.Value.GlossaryPath))
        {
            using var reader = new StreamReader(llmOptions.Value.GlossaryPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false });
            foreach (var (src, dst) in csv.GetRecords<Glossary>())
            {
                this.glossary[src] = dst;
            }
        }
    }

    private record Glossary(string Source, string Target);

    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        if (this.client is null)
        {
            throw new InvalidOperationException("LLM機能が初期化されていません。設定ダイアログからLLMオプションを設定してください");
        }
        var glossary = this.glossary.Where(kv => srcTexts.Any(s => s.SourceText.Contains(kv.Key))).ToArray();
        var common = this.common.Where(c => srcTexts.Any(s => s.SourceText.Contains(c))).ToArray();
        var sb = new StringBuilder();
        if (glossary.Length > 0)
        {
            sb.AppendLine($"""
            翻訳する際に以下の用語集を参照して、一貫した翻訳を行ってください。
            <用語集>
            {string.Join(Environment.NewLine, glossary.Select(kv => $"<用語>{kv.Key}</用語><翻訳>{kv.Value}</翻訳>"))}
            </用語集>

            """);
        }
        if (common.Length > 0)
        {
            sb.AppendLine($"""
            翻訳するテキストに以下の共通の用語が含まれている場合は、その用語のみは必ず翻訳せずにそのままの表記を利用してください。
            <共通の用語>
            {string.Join(Environment.NewLine, common)}
            </共通の用語>

            """);
        }

        var system = string.Join(Environment.NewLine, [this.preSystem, this.context, sb, this.userContext, this.postSystem]);
        if (this.isOpenAi)
        {
            return TranslateFromOpenAi(system, srcTexts);
        }
        else
        {
            return TranslateFromOther(system, srcTexts);
        }
    }

    private async ValueTask<string[]> TranslateFromOpenAi(string system, IEnumerable<TextInfo> srcs)
    {
        var client = this.client ?? throw new InvalidOperationException("LLM機能が初期化されていません。設定ダイアログからLLMオプションを設定してください");
        ChatCompletion completion = await client.CompleteChatAsync([
                ChatMessage.CreateSystemMessage(system),
                ChatMessage.CreateUserMessage(JsonSerializer.Serialize(srcs.Select(s => new { s.SourceText, s.Context }).ToArray(), jsonOptions)),
            ], openAiOptions)
            .ConfigureAwait(false);
        var res = JsonSerializer.Deserialize<Response>(completion.Content[0].Text.Trim(), jsonOptions);
        return res?.Translated ?? [];
    }

    private async ValueTask<string[]> TranslateFromOther(string system, IEnumerable<TextInfo> srcs)
    {
        var client = this.client ?? throw new InvalidOperationException("LLM機能が初期化されていません。設定ダイアログからLLMオプションを設定してください");
        var retry = 0;
        while (true)
        {
            ChatCompletion completion = await client.CompleteChatAsync([
                    ChatMessage.CreateSystemMessage(system),
                    ChatMessage.CreateUserMessage(JsonSerializer.Serialize(srcs.Select(s => new { s.SourceText, s.Context }).ToArray(), jsonOptions)),
                    assitant,
                ], otherOptions)
                .ConfigureAwait(false);
            var json = completion.Content[0].Text.Trim();
            if (!json.StartsWith('{'))
            {
                json = assitant.Content[0].Text + json;
            }
            if (!json.EndsWith('}'))
            {
                json += otherOptions.StopSequences[0];
            }
            try
            {
                var res = JsonSerializer.Deserialize<Response>(json, jsonOptions);
                return res?.Translated ?? [];
            }
            catch when (++retry < 5)
            {
                continue;
            }
        }
    }

    private record Response(string[] Translated);

    public ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        this.common = glossary.Where(kv => kv.Key == kv.Value).Select(kv => kv.Key).ToArray();
        foreach (var (key, value) in glossary.Where(kv => kv.Key != kv.Value))
        {
            this.glossary.TryAdd(key, value);
        }

        return default;
    }

    public void RegisterContext(string context)
        => this.context = $"""
        翻訳するテキストは全体を通して、以下の背景や文脈があるものして翻訳してください。
        <背景>
        {context}
        </背景>

        """;
}
