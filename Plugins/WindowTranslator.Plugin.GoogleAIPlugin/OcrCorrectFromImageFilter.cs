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
    ILogger<OcrCorrectFromTextFilter> logger)
    : OcrCorrectFilterBase<TextTarget>(langOptions, googleAiOptions, logger)
{
    protected override CorrectMode TargetMode => CorrectMode.Image;

    protected override string GetSystem(LanguageOptions languageOptions, GoogleAIOptions googleAIOptions)
        => $"""
        あなたは{CultureInfo.GetCultureInfo(languageOptions.Source).DisplayName}の専門家です。
        これから渡される複数の画像のテキストを認識して、渡された画像の順番にテキストをJson配列として出力してください。

        <出力フォーマット>
        [
            "1枚目の画像から認識したテキスト",
            "2枚目の画像から認識したテキスト",
        ]
        </出力フォーマット>
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

    protected override async Task CorrectCore(IReadOnlyList<TextTarget> texts, CancellationToken cancellationToken)
    {
        var req = new GenerateContentRequest();
        foreach (var (text, base64) in texts)
        {
            this.Cache.TryAdd(text, null);
            req.AddInlineData(base64, "image/jpeg");
        }
        try
        {
            var sw = Stopwatch.StartNew();
            var corrected = await this.Client.GenerateObjectAsync<string[]>(req, cancellationToken)
                .ConfigureAwait(false) ?? [];
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < texts.Count; i++)
            {
                this.Cache[texts[i].Original] = corrected[i];
            }
            this.Logger.LogDebug($"Correct: {sw.Elapsed}");
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, $"Failed to correct");
        }
    }

    protected override void Dropped(IReadOnlyList<TextTarget> texts)
        => this.Logger.LogDebug($"Dropped texts: {string.Join(", ", texts.Select(t => t.Original))}");
}

public record TextTarget(string Original, string Base64);
