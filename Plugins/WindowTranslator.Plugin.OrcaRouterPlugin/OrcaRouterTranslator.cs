using System.ClientModel;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.OrcaRouterPlugin.Properties;

namespace WindowTranslator.Plugin.OrcaRouterPlugin;

public sealed class OrcaRouterTranslator : ITranslateModule
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true,
    };
    private static readonly ChatCompletionOptions structuredOutputOptions = new()
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
    private readonly ChatClient client;
    private readonly bool structuredOutputSupported;
    private readonly string system;
    private readonly Dictionary<string, string> glossary = new();
    private string? context;

    public string Name { get; }

    public OrcaRouterTranslator(IOptionsSnapshot<OrcaRouterOptions> options, IOptionsSnapshot<LanguageOptions> languages)
        : this(options.Value, languages.Value, CreateClient(options.Value))
    {
    }

    internal OrcaRouterTranslator(OrcaRouterOptions options, LanguageOptions languages, ChatClient client)
    {
        this.client = client;
        this.Name = $"{nameof(OrcaRouterTranslator)}: {options.Model}";
        this.structuredOutputSupported = !options.Model.StartsWith("anthropic/", StringComparison.OrdinalIgnoreCase);
        var source = CultureInfo.GetCultureInfo(languages.Source).DisplayName;
        var target = CultureInfo.GetCultureInfo(languages.Target).DisplayName;
        this.system = $$"""
            あなたは{{source}}から{{target}}への翻訳の専門家です。
            入力の各 text を、対となる context の文脈を考慮して翻訳してください。
            入力 JSON は [{"text":"翻訳対象", "context":"文脈"}] の形式です。
            翻訳できない文字列は元の表記を維持してください。一人称は指定された表記を維持してください。
            出力は必ず {"translated":["翻訳結果"]} 形式の JSON オブジェクトのみとし、説明や Markdown は付けないでください。
            translated は入力と同じ個数・順序の文字列配列にしてください。
            <翻訳コンテキスト>{{options.TranslateContext}}</翻訳コンテキスト>
            """;
        if (File.Exists(options.GlossaryPath))
        {
            using var reader = new StreamReader(options.GlossaryPath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = false });
            foreach (var entry in csv.GetRecords<Glossary>())
            {
                this.glossary[entry.Source] = entry.Target;
            }
        }
    }

    private static ChatClient CreateClient(OrcaRouterOptions options)
        => string.IsNullOrWhiteSpace(options.ApiKey)
            ? throw new AppUserException(Resources.Text("NeedSignIn"))
            : new ChatClient(string.IsNullOrWhiteSpace(options.Model) ? OrcaRouterModels.FreeModel : options.Model,
                new ApiKeyCredential(options.ApiKey), new OpenAIClientOptions { Endpoint = new Uri(OrcaRouterModels.Endpoint) });

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        if (srcTexts.Length == 0)
        {
            return [];
        }
        var terms = this.glossary.Where(kv => srcTexts.Any(t => t.SourceText.Contains(kv.Key, StringComparison.Ordinal)))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var prompt = $"{this.system}\n<背景>{this.context}</背景>\n用語集の指定訳を使用し、原文と訳が同じ用語はそのまま出力してください。\n<用語集>{JsonSerializer.Serialize(terms, jsonOptions)}</用語集>";
        var input = JsonSerializer.Serialize(srcTexts.Select(t => new { text = t.SourceText, context = t.Context }), jsonOptions);
        ChatMessage[] messages = [
            ChatMessage.CreateSystemMessage(prompt),
            ChatMessage.CreateUserMessage(input),
        ];
        var useStructuredOutput = this.structuredOutputSupported;
        var parseFailures = 0;
        while (true)
        {
            // OrcaRouter の response_format は Anthropic 以外の対応モデルで使用する。
            // ルーターが非対応モデルを選んだ場合だけ、通常の JSON 指示へフォールバックする。
            // 末尾 assistant prefill と stop は全上流モデル共通ではないため送信しない。
            ChatCompletion completion;
            try
            {
                completion = useStructuredOutput
                    ? await this.client.CompleteChatAsync(messages, structuredOutputOptions).ConfigureAwait(false)
                    : await this.client.CompleteChatAsync(messages).ConfigureAwait(false);
            }
            catch (ClientResultException e)
            {
                var error = ReadApiError(e);
                if (IsQuotaExceeded(e, error))
                {
                    throw new AppUserException(Resources.Text("QuotaExceeded"), e);
                }
                if (useStructuredOutput && IsStructuredOutputUnsupported(e, error))
                {
                    useStructuredOutput = false;
                    continue;
                }
                throw;
            }
            var text = string.Concat(completion.Content.Select(c => c.Text));
            try
            {
                return ParseTranslation(text, srcTexts.Length);
            }
            catch (JsonException) when (++parseFailures < 5)
            {
                await Task.Delay(300).ConfigureAwait(false);
            }
        }
    }

    private static ApiError ReadApiError(ClientResultException exception)
    {
        try
        {
            var content = exception.GetRawResponse()?.Content.ToString();
            if (!string.IsNullOrWhiteSpace(content))
            {
                using var document = JsonDocument.Parse(content);
                if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                {
                    return new(
                        error.TryGetProperty("code", out var codeValue) ? codeValue.GetString() : null,
                        error.TryGetProperty("message", out var messageValue) ? messageValue.GetString() : null);
                }
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            // SDK がレスポンス本文を保持していない場合は、例外メッセージで判定する。
        }
        return default;
    }

    private static bool IsQuotaExceeded(ClientResultException exception, ApiError error)
        => error.Code is "insufficient_user_quota" or "pre_consume_token_quota_failed"
            || HasQuotaMessage(error.Message)
            || HasQuotaMessage(exception.Message);

    private static bool IsStructuredOutputUnsupported(ClientResultException exception, ApiError error)
        => exception.Status == 400
            && (error.Code == "api_not_implemented"
                || HasStructuredOutputMessage(error.Message)
                || HasStructuredOutputMessage(exception.Message));

    private static bool HasQuotaMessage(string? message)
        => message?.Contains("You've run out of credits", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("token quota is not enough", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("token cycle spend limit reached", StringComparison.OrdinalIgnoreCase) == true;

    private static bool HasStructuredOutputMessage(string? message)
        => message?.Contains("response_format", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("json_schema", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("structured output", StringComparison.OrdinalIgnoreCase) == true;

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
        var result = JsonSerializer.Deserialize<Response>(text, jsonOptions)?.Translated;
        if (result is null || result.Length != count || result.Any(t => t is null))
        {
            throw new JsonException("Translation must contain one string per input.");
        }
        return result;
    }

    public ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        foreach (var (source, target) in glossary)
        {
            this.glossary.TryAdd(source.ReplaceLineEndings(string.Empty), target.ReplaceLineEndings(string.Empty));
        }
        return default;
    }

    public void RegisterContext(string context) => this.context = context;

    private sealed record Glossary(string Source, string Target);
    private readonly record struct ApiError(string? Code, string? Message);
    private sealed record Response(string[] Translated);
}

public sealed class OrcaRouterValidator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        var selected = settings.SelectedPlugins.GetValueOrDefault(nameof(ITranslateModule)) == nameof(OrcaRouterTranslator);
        var options = settings.PluginParams.GetValueOrDefault(nameof(OrcaRouterOptions)) as OrcaRouterOptions;
        return ValueTask.FromResult(selected && string.IsNullOrWhiteSpace(options?.ApiKey)
            ? ValidateResult.Invalid("OrcaRouter", Resources.Text("NeedSignIn"))
            : ValidateResult.Valid);
    }
}
