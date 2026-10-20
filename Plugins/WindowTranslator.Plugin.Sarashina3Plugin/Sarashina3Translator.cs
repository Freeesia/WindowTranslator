using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.Sarashina3Plugin;

public sealed class Sarashina3Translator : ITranslateModule
{
    internal const string Endpoint = "https://api.sarashina.ai/v1/translate";
    private static readonly HttpClient httpClient = new();

    private readonly string apiKey;
    private readonly string sourceLanguage;
    private readonly string targetLanguage;
    private readonly HttpClient client;

    public Sarashina3Translator(IOptionsSnapshot<Sarashina3Options> options, IOptionsSnapshot<LanguageOptions> languages)
        : this(options.Value, languages.Value, httpClient)
    {
    }

    internal Sarashina3Translator(Sarashina3Options options, LanguageOptions languages, HttpClient client)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new AppUserException("Sarashina APIキーを設定してください。");
        }

        this.sourceLanguage = GetLanguageCode(languages.Source);
        this.targetLanguage = GetLanguageCode(languages.Target);
        this.apiKey = options.ApiKey.Trim();
        this.client = client;
    }

    internal static bool SupportsLanguage(string cultureCode)
        => TryGetLanguageCode(cultureCode, out _);

    private static string GetLanguageCode(string cultureCode)
        => TryGetLanguageCode(cultureCode, out var languageCode)
            ? languageCode
            : throw new AppUserException("Sarashina Translate APIは日本語と英語の翻訳に対応しています。");

    private static bool TryGetLanguageCode(string cultureCode, out string languageCode)
    {
        try
        {
            languageCode = CultureInfo.GetCultureInfo(cultureCode).TwoLetterISOLanguageName;
            return languageCode is "ja" or "en";
        }
        catch (CultureNotFoundException)
        {
            languageCode = string.Empty;
            return false;
        }
    }

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        var translated = new string[srcTexts.Length];
        for (var i = 0; i < srcTexts.Length; i++)
        {
            if (this.sourceLanguage == this.targetLanguage)
            {
                translated[i] = srcTexts[i].SourceText;
                continue;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.apiKey);
            request.Content = JsonContent.Create(new TranslateRequest(
                srcTexts[i].SourceText,
                this.sourceLanguage,
                this.targetLanguage));

            using var response = await this.client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Sarashina Translate API returned HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("translated_text", out var translatedText)
                || translatedText.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Sarashina Translate API response did not contain translated_text.");
            }

            translated[i] = translatedText.GetString()!;
        }

        return translated;
    }

    private sealed record TranslateRequest(string text, string source_lang, string target_lang);
}
