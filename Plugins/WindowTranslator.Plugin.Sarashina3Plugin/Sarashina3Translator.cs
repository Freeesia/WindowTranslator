using System.ClientModel;
using System.Globalization;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.Sarashina3Plugin;

public sealed class Sarashina3Translator : ITranslateModule
{
    internal const string Endpoint = "https://api.sarashina.ai/v1";
    internal const string Model = "sarashina3-translate";

    private readonly ChatClient client;
    private readonly string sourceLanguage;
    private readonly string targetLanguage;
    private readonly string? translateContext;
    private readonly Dictionary<string, string> glossary = new(StringComparer.Ordinal);
    private string? context;

    public Sarashina3Translator(IOptionsSnapshot<Sarashina3Options> options, IOptionsSnapshot<LanguageOptions> languages)
        : this(options.Value, languages.Value, CreateClient(options.Value))
    {
    }

    internal Sarashina3Translator(Sarashina3Options options, LanguageOptions languages)
        : this(options, languages, CreateClient(options))
    {
    }

    internal Sarashina3Translator(Sarashina3Options options, LanguageOptions languages, ChatClient client)
    {
        this.client = client;
        this.sourceLanguage = CultureInfo.GetCultureInfo(languages.Source).EnglishName;
        this.targetLanguage = CultureInfo.GetCultureInfo(languages.Target).EnglishName;
        this.translateContext = options.TranslateContext;
    }

    private static ChatClient CreateClient(Sarashina3Options options)
        => string.IsNullOrWhiteSpace(options.ApiKey)
            ? throw new AppUserException("Sarashina3 APIキーを設定してください。")
            : new ChatClient(Model, new ApiKeyCredential(options.ApiKey), new OpenAIClientOptions
            {
                Endpoint = new Uri(Endpoint),
            });

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        var translated = new string[srcTexts.Length];
        for (var i = 0; i < srcTexts.Length; i++)
        {
            var systemPrompt = $"""
                Translate from {this.sourceLanguage} to {this.targetLanguage}.
                Return only the translation without explanations or quotation marks.
                Preserve formatting, placeholders, and tags.
                <translation context>{this.translateContext}
                {this.context}
                {srcTexts[i].Context}</translation context>
                <glossary>
                {string.Join(Environment.NewLine, this.glossary.Select(entry => $"{entry.Key} => {entry.Value}"))}
                </glossary>
                """;
            var completion = await this.client.CompleteChatAsync([
                ChatMessage.CreateSystemMessage(systemPrompt),
                ChatMessage.CreateUserMessage(srcTexts[i].SourceText),
            ]).ConfigureAwait(false);
            translated[i] = string.Concat(completion.Value.Content.Select(content => content.Text)).Trim();
        }

        return translated;
    }

    public ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        foreach (var (source, target) in glossary)
        {
            this.glossary.TryAdd(source.ReplaceLineEndings(string.Empty), target.ReplaceLineEndings(string.Empty));
        }

        return default;
    }

    public void RegisterContext(string context)
        => this.context = context;
}
