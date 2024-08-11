using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.LLMPlugin;

[DisplayName("LLM")]
public class LLMTranslator(IOptionsSnapshot<LLMOptions> llmOptions, IOptionsSnapshot<LanguageOptions> langOptions) : ITranslateModule
{
    private readonly ChatClient client = new(
                llmOptions.Value.Model ?? string.Empty,
                llmOptions.Value.ApiKey ?? string.Empty,
                llmOptions.Value.Endpoint is { Length: > 0 } e ? new OpenAIClientOptions() { Endpoint = new(e) } : null);
    private readonly ChatMessage system = ChatMessage.CreateSystemMessage($"""
        あなたは{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}から{CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName}へ翻訳するの専門家です。
        入力テキストは{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}のテキストであり、翻訳が必要です。
        渡されたテキストを{CultureInfo.GetCultureInfo(langOptions.Value.Target).DisplayName}へ翻訳して出力してください。
        入力テキストは以下のJsonフォーマットになっています。出力テキストも同じJsonフォーマットで、入力テキストの順序を維持してください。

        <入力テキストのJsonフォーマット>
        ["翻訳するテキスト1","翻訳するテキスト2"]
        </入力テキストのフォーマット>
        """);
    private static readonly ChatMessage assitant = ChatMessage.CreateAssistantMessage("[\"");

    public async ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        var completion = await this.client.CompleteChatAsync([
            this.system,
            ChatMessage.CreateUserMessage(JsonSerializer.Serialize(srcTexts)),
            assitant,
        ], new()
        {
            StopSequences = { "\"]" }
        }).ConfigureAwait(false);
        var json = assitant.Content[0].Text + completion.Value.ToString().Trim()+ "\"]";
        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }
}
