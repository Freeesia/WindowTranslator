using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.LLMPlugin;

public class OcrCorrectionFilter : IFilterModule
{
    private readonly ConcurrentDictionary<string, string?> cache = new();
    private readonly ChatClient? client;
    private readonly ChatMessage system;
    private static readonly ChatMessage assitant = ChatMessage.CreateAssistantMessage("\"");
    private readonly ILogger<OcrCorrectionFilter> logger;

    public OcrCorrectionFilter(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<LLMOptions> llmOptions, ILogger<OcrCorrectionFilter> logger)
    {
        if (llmOptions.Value.Model is { Length: > 0 } model && llmOptions.Value.ApiKey is { Length: > 0 } apiKey)
        {
            this.client = new(
                model,
                apiKey,
                llmOptions.Value.Endpoint is { Length: > 0 } e ? new OpenAIClientOptions() { Endpoint = new(e) } : null);
        }
        this.system = ChatMessage.CreateSystemMessage($"""
        あなたは{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}の専門家です。
        これから渡される文字列はOCRによって認識されたものであり、誤字や脱字が含まれています。
        次の指示に従って、渡された文字列を修正してください。

        1. 誤字や脱字を、文字形の類似性を考慮して修正してください。
        2. 単語や文章の意味が崩れないように、文字数の変更は最小限にしてください。
        3. 正しい単語や文章に修正した結果のみを出力してください。
        4. 誤字や脱字がない場合は、そのままの文字列を出力してください。
        5. 単語や文章として認識できない場合は、空文字を出力してください。

        <誤字修正の例>
        {llmOptions.Value.CorrectSample}
        </誤字修正の例>

        修正する文字列は以下のJsonフォーマットになっています。出力文字列も同じJsonフォーマットで、入力文字列の順序を維持してください。
        <入力テキストのJsonフォーマット>
        "修正する文字列1"
        </入力テキストのJsonフォーマット>
        """);
        this.logger = logger;
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts)
        => texts;

    public IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts)
        => texts.Select(text =>
        {
            if (!this.cache.TryGetValue(text.Text, out var corrected))
            {
                Correct(text.Text);
            }
            if (corrected is not null)
            {
                return text.Text != corrected ? text with { Text = corrected } : text;
            }
            return text;
        })
        .Where(t => !string.IsNullOrEmpty(t.Text));

    private async void Correct(string text)
    {
        if (this.client is null)
        {
            return;
        }
        try
        {
            this.cache[text] = null;
            var completion = await this.client.CompleteChatAsync([
                this.system,
                ChatMessage.CreateUserMessage(JsonSerializer.Serialize(text)),
                assitant,
            ], new()
            {
                StopSequences = { "\"" }
            }).ConfigureAwait(false);
            this.cache[text] = completion.Value.ToString().Trim();
        }
        catch (Exception e)
        {
            this.logger.LogError(e, $"Failed to correct `{text}`");
        }
    }
}
