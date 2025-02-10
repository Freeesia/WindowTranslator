using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Quickenshtein;
using WindowTranslator.Extensions;

namespace WindowTranslator.Modules.Ocr;

public class OcrBufferFilter(IOptions<WindowsMediaOcrParam> options, ILogger<OcrBufferFilter> logger) : IFilterModule
{
    private static readonly ObjectPool<List<TextRect>> listPool = ObjectPool.Create(new ListPolicy());
    private readonly ILogger<OcrBufferFilter> logger = logger;
    private readonly Queue<List<TextRect>> buffer = new();
    private readonly int bufferSize = options.Value.BufferSize;

    public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts)
    {
        if (this.bufferSize <= 0)
        {
            await foreach (var text in texts)
            {
                yield return text;
            }
            yield break;
        }
        using var l = this.logger.LogDebugTime("OcrBufferFilter");
        // バッファ内の全テキストを集め、AreSimilarで重複チェックして重複を排除
        var bufferedTexts = listPool.Get();
        foreach (var rect in this.buffer.SelectMany(b => b))
        {
            if (!bufferedTexts.Any(existing => AreSimilar(existing, rect)))
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
        await foreach (var text in texts)
        {
            currentTextsList.Add(text);
            yield return text;
        }

        // バッファに現在のテキストを追加
        this.buffer.Enqueue(currentTextsList);

        // finalBufferedCacheを再利用: 事前にクリアしてから、bufferedTextsから候補を追加
        var finalBuffered = listPool.Get();
        foreach (var bufferedText in bufferedTexts)
        {
            if (!currentTextsList.Any(t => AreSimilar(t, bufferedText) || Intersects(t, bufferedText)) &&
                !finalBuffered.Any(existing => Intersects(existing, bufferedText)))
            {
                finalBuffered.Add(bufferedText);
                this.logger.LogDebug($"Buffered: {bufferedText.Text}");
                yield return bufferedText;
            }
        }

        // リストを返却
        listPool.Return(bufferedTexts);
        listPool.Return(finalBuffered);
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts) => texts;

    // 矩形同士が重なっているかを判定する追加メソッド
    private static bool Intersects(TextRect rect1, TextRect rect2)
        => rect1.X < rect2.X + rect2.Width &&
            rect1.X + rect1.Width > rect2.X &&
            rect1.Y < rect2.Y + rect2.Height &&
            rect1.Y + rect1.Height > rect2.Y;

    private static bool AreSimilar(TextRect rect1, TextRect rect2)
    {
        // 位置とサイズのずれが10未満
        var xDiff = Math.Abs(rect1.X - rect2.X);
        var yDiff = Math.Abs(rect1.Y - rect2.Y);
        var widthDiff = Math.Abs(rect1.Width - rect2.Width);
        var heightDiff = Math.Abs(rect1.Height - rect2.Height);

        if (xDiff >= 10 || yDiff >= 10 || widthDiff >= 10 || heightDiff >= 10)
        {
            return false;
        }

        // テキストの内容が90%以上一致
        var p = (float)Levenshtein.GetDistance(rect1.Text, rect2.Text, CalculationOptions.DefaultWithThreading)
            / Math.Max(rect1.Text.Length, rect2.Text.Length);

        return p >= 0.9;
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
