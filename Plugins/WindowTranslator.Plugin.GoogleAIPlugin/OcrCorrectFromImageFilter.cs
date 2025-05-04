using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public sealed class OcrCorrectFromImageFilter(
    IOptionsSnapshot<LanguageOptions> langOptions,
    IOptionsSnapshot<GoogleAIOptions> googleAiOptions,
    ILogger<OcrCorrectFromImageFilter> logger)
    : OcrCorrectFilterBase<TextTarget>(langOptions, googleAiOptions, logger)
{
    protected override CorrectMode TargetMode => CorrectMode.Image;

    protected override string GetSystem(LanguageOptions languageOptions, GoogleAIOptions googleAIOptions)
        => $$"""
        あなたは{{CultureInfo.GetCultureInfo(languageOptions.Source).DisplayName}}のテキストを認識可能なOCR（光学文字認識）を実行するAIです。
        入力された複数の画像を、送信された順番（1番目、2番目、3番目...）に従って処理してください。
        各画像からテキストを抽出し、以下の**厳密なJSON形式**で結果のみを出力してください。

        ```json
        [
          {
            "image_index": 1,
            "ocr_text": "[画像1から認識されたテキスト または 認識不可の場合は空文字]"
          },
          {
            "image_index": 2,
            "ocr_text": "[画像2から認識されたテキスト または 認識不可の場合は空文字]"
          },
          {
            "image_index": 3,
            "ocr_text": "[画像3から認識されたテキスト または 認識不可の場合は空文字]"
          },
          // ... 入力された画像の数だけオブジェクトが続く
        ]
        ```
        `image_index` キーには、入力画像の順番（1から始まる整数）を出力してください。
        `ocr_text` キーには、認識されたテキストを出力してください。もしテキストが認識できなかった場合は、 空文字列を出力してください。
        JSON配列の要素数は、入力された画像の枚数と必ず一致しなければならないことに注意してください。
        """;

    protected override async ValueTask<IReadOnlyList<TextTarget>> GetQueueData(IEnumerable<TextRect> targets, FilterContext context)
    {
        var sw = Stopwatch.StartNew();
        var list = new List<TextTarget>();
        var max = new Rectangle(0, 0, context.ImageSize.Width, context.ImageSize.Height);
        foreach (var rect in targets)
        {
            var r = rect.ToRect();
            r.Inflate((int)(r.Width * 0.1), (int)(r.Height * 0.1));
            r.Intersect(max);
            list.Add(new(rect.Text, await context.SoftwareBitmap.EncodeToJpegBase64(r).ConfigureAwait(false)));
        }
        this.Logger.LogDebug($"Image to CropedBase64: {sw.Elapsed}");
        return list;
    }

    private record RecognizedText(int ImageIndex, string OcrText);

    protected override async Task CorrectCore(IReadOnlyList<TextTarget> texts, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await Parallel.ForEachAsync(texts.Chunk(5), cancellationToken, async (chunk, ct) =>
        {
            var req = new GenerateContentRequest();
            foreach (var (text, base64) in chunk)
            {
                this.Cache.TryAdd(text, null);
                req.AddInlineData(base64, "image/jpeg");
            }
            RecognizedText[] corrected;
            try
            {
                corrected = await this.Client.GenerateObjectAsync<RecognizedText[]>(req, cancellationToken)
                    .ConfigureAwait(false) ?? [];
                cancellationToken.ThrowIfCancellationRequested();
                Array.Sort(corrected, (x, y) => x.ImageIndex - y.ImageIndex);
                var original = chunk.Select(t => t.Original).ToArray();
                for (var i = 0; i < original.Length; i++)
                {
                    this.Cache[original[i]] = corrected[i].OcrText;
                }
            }
            catch (Exception e)
            {
                this.Logger.LogError(e, $"Failed to correct");
            }
        }).ConfigureAwait(false);
        this.Logger.LogDebug($"Correct: {sw.Elapsed}");
    }

    protected override void Dropped(IReadOnlyList<TextTarget> texts)
        => this.Logger.LogDebug($"Dropped texts: {string.Join(", ", texts.Select(t => t.Original))}");
}

public record TextTarget(string Original, string Base64);
