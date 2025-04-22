using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public sealed class GoogleAIOcr : IOcrModule, IDisposable
{
    private readonly ILogger<GoogleAIOcr> logger;
    private readonly GenerativeModel client;
    private readonly InMemoryRandomAccessStream stream = new();

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

    public void Dispose()
        => this.stream.Dispose();

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        var base64 = await EncodeToJpegBase64(bitmap).ConfigureAwait(false);
        var req = new GenerateContentRequest();
        req.AddInlineData(base64, "image/jpeg");
        var res = await this.client.GenerateObjectAsync<Recct[]>(req).ConfigureAwait(false) ?? [];
        var results = new List<TextRect>();
        // 画像のピクセルサイズ
        var imageWidth = (double)bitmap.PixelWidth;
        var imageHeight = (double)bitmap.PixelHeight;
        foreach (var rect in res)
        {
            if (rect.Box2d.Length != 4 || rect.Box2d.Any(x => x < 0 || x > 1000))
            {
                // Box2dの長さが4でない、または値が[0..1000]でない場合は無視する
                this.logger.LogWarning("Invalid box2d length: {Box2d}", string.Join(", ", rect.Box2d));
                continue;
            }
            // 正規化値 [0..1000] をピクセル値に変換
            var yMinNorm = rect.Box2d[0];
            var xMinNorm = rect.Box2d[1];
            var yMaxNorm = rect.Box2d[2];
            var xMaxNorm = rect.Box2d[3];
            var xMinPx = xMinNorm / 1000.0 * imageWidth;
            var yMinPx = yMinNorm / 1000.0 * imageHeight;
            var xMaxPx = xMaxNorm / 1000.0 * imageWidth;
            var yMaxPx = yMaxNorm / 1000.0 * imageHeight;
            var widthPx = xMaxPx - xMinPx;
            var heightPx = yMaxPx - yMinPx;
            results.Add(new TextRect(rect.Text,
                xMinPx,
                yMinPx,
                widthPx,
                heightPx,
                heightPx,
                false));
        }
        return results;
    }

    private async Task<string> EncodeToJpegBase64(SoftwareBitmap bitmap)
    {
        this.stream.Seek(0);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, this.stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        this.stream.Seek(0);
        using var mem = MemoryPool<byte>.Shared.Rent((int)this.stream.Size);
        var buffer = mem.Memory[..(int)this.stream.Size];
        await this.stream.AsStreamForRead().ReadExactlyAsync(buffer).ConfigureAwait(false);
        return Convert.ToBase64String(buffer.Span);
    }

    private record Recct(int[] Box2d, string Text);
}
