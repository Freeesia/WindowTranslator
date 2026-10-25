using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.Sarashina3Plugin;

public sealed class Sarashina3Translator : ITranslateModule
{
    internal const string Endpoint = "https://api.sarashina.ai/v1/translate";
    internal const int MaxTextLength = 5000;
    private const int MaxParseAttempts = 3;
    private const int MaxRateLimitAttempts = 5;
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true,
    };
    private static readonly ChatCompletionOptions structuredOutputOptions = new()
    {
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            "translated_array",
            BinaryData.FromString("""
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
                """),
            "翻訳後のテキストの配列",
            true),
    };
    internal const string ChatModel = "sarashina3-mini";
    internal const string ChatEndpoint = "https://api.sarashina.ai/v1";
    // 429の待機時間を含めるため、既定の100秒より長くする
    private static readonly TimeSpan requestTimeout = TimeSpan.FromMinutes(10);
    private static readonly HttpClient httpClient = new() { Timeout = requestTimeout };

    private readonly string apiKey;
    private readonly string sourceLanguage;
    private readonly string targetLanguage;
    private readonly bool useTranslateApi;
    private readonly HttpClient client;
    private readonly ChatClient chatClient;
    private readonly string system;

    internal Func<TimeSpan, Task> Delay { get; set; } = Task.Delay;

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

        this.sourceLanguage = CultureInfo.GetCultureInfo(languages.Source).TwoLetterISOLanguageName;
        this.targetLanguage = CultureInfo.GetCultureInfo(languages.Target).TwoLetterISOLanguageName;
        this.useTranslateApi = IsTranslateApiLanguage(this.sourceLanguage) && IsTranslateApiLanguage(this.targetLanguage);
        this.apiKey = options.ApiKey.Trim();
        this.client = client;
        this.chatClient = new ChatClient(ChatModel, new ApiKeyCredential(this.apiKey), new OpenAIClientOptions
        {
            Endpoint = new Uri(ChatEndpoint),
            Transport = new HttpClientPipelineTransport(client),
            NetworkTimeout = requestTimeout,
        });
        var source = CultureInfo.GetCultureInfo(languages.Source).DisplayName;
        var target = CultureInfo.GetCultureInfo(languages.Target).DisplayName;
        this.system = $$"""
            あなたは{{source}}から{{target}}への翻訳の専門家です。
            入力の各 text を、対となる context の文脈を考慮して{{target}}に翻訳してください。
            入力 JSON は [{"text":"翻訳対象", "context":"文脈"}] の形式です。
            翻訳できない文字列は元の表記を維持してください。
            出力は必ず {"translated":["翻訳結果"]} 形式の JSON オブジェクトのみとし、説明や Markdown は付けないでください。
            translated は入力と同じ個数・順序の文字列配列にしてください。
            """;
    }

    // 専用のTranslate APIは日本語と英語の相互翻訳のみ対応している
    private static bool IsTranslateApiLanguage(string languageCode)
        => languageCode is "ja" or "en";

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        if (this.sourceLanguage == this.targetLanguage)
        {
            return [.. srcTexts.Select(t => t.SourceText)];
        }

        if (!this.useTranslateApi)
        {
            return srcTexts.Length == 0 ? [] : await this.TranslateWithChatAsync(srcTexts).ConfigureAwait(false);
        }

        var translated = new string[srcTexts.Length];
        for (var i = 0; i < srcTexts.Length; i++)
        {
            var builder = new StringBuilder();
            foreach (var chunk in SplitText(srcTexts[i].SourceText))
            {
                builder.Append(await this.TranslateChunkAsync(chunk).ConfigureAwait(false));
            }

            translated[i] = builder.ToString();
        }

        return translated;
    }

    private async ValueTask<string[]> TranslateWithChatAsync(TextInfo[] srcTexts)
    {
        var input = JsonSerializer.Serialize(
            srcTexts.Select(t => new { text = t.SourceText, context = t.Context }),
            jsonOptions);
        ChatMessage[] messages = [new SystemChatMessage(this.system), new UserChatMessage(input)];
        var parseFailures = 0;
        var rateLimits = 0;
        while (true)
        {
            ChatCompletion completion;
            try
            {
                completion = await this.chatClient.CompleteChatAsync(messages, structuredOutputOptions).ConfigureAwait(false);
            }
            catch (ClientResultException e) when (e.Status == 429 && ++rateLimits < MaxRateLimitAttempts)
            {
                double? seconds = e.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var value) == true
                    && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : null;
                await this.Delay(GetRetryDelay(seconds)).ConfigureAwait(false);
                continue;
            }

            try
            {
                return ParseTranslation(string.Concat(completion.Content.Select(c => c.Text)), srcTexts.Length);
            }
            catch (JsonException) when (++parseFailures < MaxParseAttempts)
            {
            }
        }
    }

    // レート制限はカウント期間(60秒)ごとにリセットされるため、Retry-Afterの秒数だけ待つ
    private static TimeSpan GetRetryDelay(double? retryAfterSeconds)
        => TimeSpan.FromSeconds(Math.Clamp(retryAfterSeconds ?? 60, 1, 120));

    // 構造化出力でもスキーマ準拠は保証されないため、件数も検証する
    internal static string[] ParseTranslation(string text, int count)
    {
        text = text.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal) && text.EndsWith("```", StringComparison.Ordinal))
        {
            var newline = text.IndexOf('\n');
            if (newline >= 0)
            {
                text = text[(newline + 1)..^3].Trim();
            }
        }

        var result = JsonSerializer.Deserialize<ChatResponse>(text, jsonOptions)?.Translated;
        if (result is null || result.Length != count || result.Any(t => t is null))
        {
            throw new JsonException("Translation must contain one string per input.");
        }

        return result;
    }

    private async ValueTask<string> TranslateChunkAsync(string text)
    {
        HttpResponseMessage response;
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.apiKey);
            request.Content = JsonContent.Create(new TranslateRequest(text, this.sourceLanguage, this.targetLanguage));

            response = await this.client.SendAsync(request).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= MaxRateLimitAttempts)
            {
                break;
            }

            var wait = GetRetryDelay(response.Headers.RetryAfter?.Delta?.TotalSeconds);
            response.Dispose();
            await this.Delay(wait).ConfigureAwait(false);
        }

        using var _ = response;
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

        return translatedText.GetString()!;
    }

    // 上限はUnicodeコードポイント単位のため、サロゲートペアを分断しないよう分割する
    internal static IEnumerable<string> SplitText(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var end = start;
            var count = 0;
            while (end < text.Length && count < MaxTextLength)
            {
                end += char.IsHighSurrogate(text[end]) && end + 1 < text.Length && char.IsLowSurrogate(text[end + 1]) ? 2 : 1;
                count++;
            }

            yield return text[start..end];
            start = end;
        }
    }

    private sealed record ChatResponse(string[] Translated);
    private sealed record TranslateRequest(string text, string source_lang, string target_lang);
}
