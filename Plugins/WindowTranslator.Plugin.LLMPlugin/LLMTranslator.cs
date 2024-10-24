using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.LLMPlugin;

[DisplayName("LLM")]
public class LLMTranslator : ITranslateModule
{
    private readonly ChatClient? client;
    private readonly ChatMessage system;
    private static readonly ChatMessage assitant = ChatMessage.CreateAssistantMessage("[\"");

    public LLMTranslator(IOptionsSnapshot<LLMOptions> llmOptions, IOptionsSnapshot<LanguageOptions> langOptions)
    {
        this.system = ChatMessage.CreateSystemMessage($"""
        あなたは{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}から{CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName}へ翻訳するの専門家です。
        入力テキストは{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}のテキストであり、翻訳が必要です。
        渡されたテキストを{CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName}へ翻訳して出力してください。
        入力テキストは以下のJsonフォーマットになっています。出力テキストも同じJsonフォーマットで、入力テキストの順序を維持してください。

        <入力テキストのJsonフォーマット>
        ["翻訳するテキスト1","翻訳するテキスト2"]
        </入力テキストのフォーマット>

        <翻訳の例>
        {llmOptions.Value.TranslateSample}
        </翻訳の例>
        """);
        if (llmOptions.Value.Model is { Length: > 0 } model && llmOptions.Value.ApiKey is { Length: > 0 } key)
        {
            this.client = new(model, new(key), llmOptions.Value.Endpoint is { Length: > 0 } e ? new OpenAIClientOptions() { Endpoint = new(e) } : null);
        }
    }

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        if (this.client is null)
        {
            throw new InvalidOperationException("LLM機能が初期化されていません。設定ダイアログからLLMオプションを設定してください");
        }
        ChatCompletion completion = await this.client.CompleteChatAsync([
            this.system,
            ChatMessage.CreateUserMessage(JsonSerializer.Serialize(srcTexts.Select(s => s.Text))),
            assitant,
        ], new()
        {
            StopSequences = { "\"]" }
        }).ConfigureAwait(false);
        var json = assitant.Content[0].Text + completion.Content[0].Text.Trim() + "\"]";
        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }
}
