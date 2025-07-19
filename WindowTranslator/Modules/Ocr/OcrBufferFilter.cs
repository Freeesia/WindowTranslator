using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Quickenshtein;
using WindowTranslator.Extensions;

namespace WindowTranslator.Modules.Ocr;

public class OcrBufferFilter(IOptions<BasicOcrParam> options, ILogger<OcrBufferFilter> logger) : IFilterModule
{
    private static readonly ObjectPool<List<TextRect>> listPool = ObjectPool.Create(new ListPolicy());
    private readonly ILogger<OcrBufferFilter> logger = logger;
    private readonly Queue<List<TextRect>> buffer = new();
    private readonly int bufferSize = options.Value.BufferSize;
    private readonly bool isSuppressVibe = options.Value.IsSuppressVibe;
    private readonly bool isEnableRecover = options.Value.IsEnableRecover;

    public double Priority => FilterPriority.OcrBufferFilter;

    public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
    {
        if (this.bufferSize <= 0)
        {
            await foreach (var text in texts.ConfigureAwait(false))
            {
                yield return text;
            }
            yield break;
        }
        using var l = this.logger.LogDebugTime("OcrBufferFilter");
        var threshold = (context.ImageSize * 0.02f).ToSize();
        // バッファ内の全テキストを集め、AreSimilarで重複チェックして重複を排除
        var bufferedTexts = listPool.Get();
        foreach (var rect in this.buffer.SelectMany(b => b))
        {
            if (!bufferedTexts.Any(existing => AreSimilar(existing, rect, threshold)))
            {
                bufferedTexts.Add(rect);
            }
        }

        List<TextRect> currentTextsList;
        if (buffer.Count == this.bufferSize)
        {
            // 既にプールされたリストを再利用
            currentTextsList = buffer.Dequeue();
            currentTextsList.Clear();
        }
        else
        {
            // 新規作成ではなく、プールから取得
            currentTextsList = listPool.Get();
        }

        // 現在のテキストを列挙しながら処理
        await foreach (var t in texts.ConfigureAwait(false))
        {
            var text = t;
            // 過去のバッファ内に類似するテキストがあるか確認
            // もしあって、かつフォントサイズが異なる場合は過去のテキストのフォントサイズを使用
            if (this.isSuppressVibe && bufferedTexts.FirstOrDefault(buf => AreSimilar(buf, text, threshold)) is { } pastText)
            {
                // フォントサイズを平均化
                text = text with
                {
                    X = pastText.X,
                    Y = pastText.Y,
                    Width = Math.Max(text.Width, pastText.Width),
                    Height = Math.Max(text.Height, pastText.Height),
                    FontSize = pastText.FontSize
                };
            }
            currentTextsList.Add(text);
            yield return text;
        }

        // バッファに現在のテキストを追加
        this.buffer.Enqueue(currentTextsList);

        // finalBufferedCacheを再利用: 事前にクリアしてから、bufferedTextsから候補を追加
        var finalBuffered = listPool.Get();
        if (this.isEnableRecover)
        {
            foreach (var buf in bufferedTexts)
            {
                if (!currentTextsList.Any(t => AreSimilar(t, buf, threshold) || Intersects(t, buf)) &&
                    !finalBuffered.Any(existing => Intersects(existing, buf)))
                {
                    finalBuffered.Add(buf);
                    this.logger.LogDebug($"Buffered: {buf.Text}");
                    yield return buf;
                }
            }
        }

        // リストを返却
        listPool.Return(bufferedTexts);
        listPool.Return(finalBuffered);
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context) => texts;

    // 矩形同士が重なっているかを判定する追加メソッド
    private static bool Intersects(TextRect rect1, TextRect rect2)
        => rect1.X < rect2.X + rect2.Width &&
            rect1.X + rect1.Width > rect2.X &&
            rect1.Y < rect2.Y + rect2.Height &&
            rect1.Y + rect1.Height > rect2.Y;

    private static bool AreSimilar(TextRect rect1, TextRect rect2, Size threshold)
    {
        // テキストの一致率が80%未満なら別扱い
        var p = 1 - ((float)Levenshtein.GetDistance(rect1.Text, rect2.Text, CalculationOptions.DefaultWithThreading)
            / Math.Max(rect1.Text.Length, rect2.Text.Length));

        if (p < 0.8)
        {
            return false;
        }

        // 位置とサイズのずれがしきい値以下だったら同じとみなす
        return Math.Abs(rect1.X - rect2.X) <= threshold.Width &&
            Math.Abs(rect1.Y - rect2.Y) <= threshold.Height &&
            Math.Abs(rect1.Width - rect2.Width) <= threshold.Width &&
            Math.Abs(rect1.Height - rect2.Height) <= threshold.Height;
    }
}

file class ListPolicy : IPooledObjectPolicy<List<TextRect>>
{
    public List<TextRect> Create() => [];

    public bool Return(List<TextRect> obj)
    {
        obj.Clear();
        return true;
    }
}
