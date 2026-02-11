using System.ClientModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Windows.Graphics.Imaging;
using WindowTranslator.Extensions;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.LLMPlugin;

[Experimental("WT0001")]
[DisplayName("ChatGPT API")]
public sealed class LLMOcr : IOcrModule
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    private static readonly ChatCompletionOptions ocrOptions = new()
    {
        ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            "recognized_texts",
            BinaryData.FromBytes("""
            {
                "type": "object",
                "properties": {
                    "texts": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "box_2d": {
                                    "type": "array",
                                    "items": { "type": "integer" },
                                    "minItems": 4,
                                    "maxItems": 4
                                },
                                "text": { "type": "string" }
                            },
                            "additionalProperties": false,
                            "required": ["box_2d", "text"]
                        }
                    },
                    "text": { "type": "string" }
                },
                "additionalProperties": false,
                "required": ["texts"]
            }
            """u8.ToArray()),
            "認識されたテキストと位置情報の配列",
            false),
    };
    private static readonly AssistantChatMessage assistant = ChatMessage.CreateAssistantMessage("{");
    private readonly ILogger<LLMOcr> logger;
    private readonly SystemChatMessage system;
    private readonly ChatClient client;

    public LLMOcr(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<LLMOptions> llmOptions, ILogger<LLMOcr> logger)
    {
        var options = llmOptions.Value;
        this.logger = logger;

        if (string.IsNullOrEmpty(options.ApiKey) || string.IsNullOrEmpty(options.Model))
        {
            throw new AppUserException("LLM機能が初期化されていません。設定ダイアログからLLMオプションを設定してください");
        }

        this.system = ChatMessage.CreateSystemMessage($$"""
        あなたは{{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}}の専門家です。
        これから渡される画像内のテキストを認識して、テキストごとの位置情報と認識したテキストをJson形式で出力してください。
        また、以下の点に注意してください。

        1. テキストには改行を含めないでください。
        2. 複数行のテキストは1行ごとに分割してください。
        3. 表形式のテキストは行ごと、列ごとに分割してください。
        4. 座標値は画像ごとに0～1000に正規化してください。
        5. 認識できない場合は、空文字を出力してください。
        6. 数字のみのテキストは認識しないでください。

        出力形式:
        ```json
        {
          "texts": [
            {"box_2d": [y_min, x_min, y_max, x_max], "text": "認識したテキスト1"},
            {"box_2d": [y_min, x_min, y_max, x_max], "text": "認識したテキスト2"}
          ]
        }
        ```
        """);

        var clientOptions = options.Endpoint is { Length: > 0 } e ? new OpenAI.OpenAIClientOptions() { Endpoint = new(e) } : null;
        this.client = new(
            options.Model,
            new ApiKeyCredential(options.ApiKey),
            clientOptions);
    }

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        var bytes = await bitmap.EncodeToJpegBytes().ConfigureAwait(false);
        var image = BinaryData.FromBytes(bytes);

        var messages = new List<ChatMessage>
        {
            this.system,
            ChatMessage.CreateUserMessage([ChatMessageContentPart.CreateImagePart(image, "image/jpeg")])
        };

        try
        {
            ChatCompletion completion = await this.client.CompleteChatAsync(messages, ocrOptions)
                .ConfigureAwait(false);

            var json = completion.Content[0].Text.Trim();
            if (!json.StartsWith('{'))
            {
                json = "{" + json;
            }
            if (!json.EndsWith('}'))
            {
                json += "}";
            }
            var rectangles = JsonSerializer.Deserialize<RecognizedTexts>(json, jsonOptions)?.Texts ?? [];

            // 画像のピクセルサイズ
            var imageWidth = bitmap.PixelWidth;
            var imageHeight = bitmap.PixelHeight;

            return rectangles
                .Where(rect => rect.Box2d.Length == 4 && rect.Box2d.All(v => v >= 0 && v <= 1000))
                .Select(rect =>
                {
                    var yMinPx = (int)(rect.Box2d[0] / 1000.0 * imageHeight);
                    var xMinPx = (int)(rect.Box2d[1] / 1000.0 * imageWidth);
                    var yMaxPx = (int)(rect.Box2d[2] / 1000.0 * imageHeight);
                    var xMaxPx = (int)(rect.Box2d[3] / 1000.0 * imageWidth);
                    var widthPx = xMaxPx - xMinPx;
                    var heightPx = yMaxPx - yMinPx;
                    return new TextRect(rect.Text, xMinPx, yMinPx, widthPx, heightPx, heightPx, false);
                });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "OCR処理中にエラーが発生しました");
            return [];
        }
    }

    private record Rect([property: JsonPropertyName("box_2d")] int[] Box2d, string Text);
    private record RecognizedTexts(Rect[] Texts);
}