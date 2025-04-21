using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
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


        <出力フォーマット>
        [
          {"box_2d": [left, top, right, bottom], "text": "認識したテキスト1"},
          {"box_2d": [left, top, right, bottom], "text": "認識したテキスト2"}
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
        this.stream.Seek(0);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, this.stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        this.stream.Seek(0);
        var mem = MemoryPool<byte>.Shared.Rent((int)this.stream.Size);
        var buffer = mem.Memory[..(int)this.stream.Size];
        await this.stream.AsStreamForRead().ReadExactlyAsync(buffer).ConfigureAwait(false);
        var base64 = Convert.ToBase64String(buffer.Span);
        var req = new GenerateContentRequest();
        req.AddInlineData(base64, "image/jpeg");
        var res = await this.client.GenerateObjectAsync<Recct[]>(req).ConfigureAwait(false) ?? Array.Empty<Recct>();
        var results = new List<TextRect>();
        foreach (var rect in res)
        {
            if (rect.Box2d.Length != 4)
            {
                this.logger.LogWarning("Invalid box2d length: {Box2d}", string.Join(", ", rect.Box2d));
                continue;
            }
            results.Add(new TextRect(rect.Text,
                rect.Box2d[0],
                rect.Box2d[1],
                rect.Box2d[2] - rect.Box2d[0],
                rect.Box2d[3] - rect.Box2d[1],
                rect.Box2d[3] - rect.Box2d[1],
                false));
        }
        return results;
    }

    private record Recct(int[] Box2d, string Text);
}
