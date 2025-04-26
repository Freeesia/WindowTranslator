using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

[Experimental("WT0001")]
[DisplayName("Google AI")]
public sealed class GoogleAIOcr : IOcrModule
{
    private readonly ILogger<GoogleAIOcr> logger;
    private readonly GenerativeModel client;

    public GoogleAIOcr(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GoogleAIOptions> googleAiOptions, ILogger<GoogleAIOcr> logger)
    {
        var options = googleAiOptions.Value;
        var system = $$"""
        あなたは{{CultureInfo.GetCultureInfo(langOptions.Value.Source).DisplayName}}の専門家です。
        これから渡される画像内のテキストを認識して、テキストごとの位置情報と認識したテキストをJson形式で出力してください。
        また、以下の点に注意してください。

        1. テキストには改行を含めないでください。
        2. 複数行のテキストは1行ごとに分割してください。
        3. 表形式のテキストは行ごと、列ごとに分割してください。
        4. 座標値は画像ごとに0～1000に正規化してください。

        <出力フォーマット>
        [
          {"box_2d": [y_min, x_min, y_max, x_max], "text": "認識したテキスト1"},
          {"box_2d": [y_min, x_min, y_max, x_max], "text": "認識したテキスト2"}
        ]
        </出力フォーマット>
        """;
        this.logger = logger;
        var googleAI = new GoogleAi(options.ApiKey, logger: logger);
        this.client = googleAI.CreateGenerativeModel(
            string.IsNullOrEmpty(options.PreviewModel) ? options.Model.GetName() : options.PreviewModel,
            safetyRatings: [
                new(){ Category = HarmCategory.HARM_CATEGORY_HARASSMENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_HATE_SPEECH, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_SEXUALLY_EXPLICIT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_DANGEROUS_CONTENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
            ],
            systemInstruction: system);
    }

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        var base64 = await bitmap.EncodeToJpegBase64().ConfigureAwait(false);
        var req = new GenerateContentRequest();
        req.AddInlineData(base64, "image/jpeg");
        var res = await this.client.GenerateObjectAsync<Recct[]>(req).ConfigureAwait(false) ?? [];
        // 画像のピクセルサイズ
        var imageWidth = bitmap.PixelWidth;
        var imageHeight = bitmap.PixelHeight;
        return res
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

    private record Recct(int[] Box2d, string Text);
}
