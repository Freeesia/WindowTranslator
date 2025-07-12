namespace WindowTranslator.Modules.Ocr;

public class OcrCommonFilter : IFilterModule
{
    public double Priority => FilterPriority.OcrCommonFilter;

    public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
    {
        // 矩形を右方向に伸ばして他の矩形と被らないようにMaxWidthを設定する
        var rectList = await texts.ToArrayAsync().ConfigureAwait(false);
        for (int i = 0; i < rectList.Length; i++)
        {
            var r = rectList[i].GetRotatedBoundingBox();
            // 右側にある矩形を探す
            var maxRight = rectList
                .Select(other => other.GetRotatedBoundingBox())
                .Where((other, idx) => idx != i)
                // 縦方向で重なっている右側にある矩形
                .Where(other => other.Top < r.Bottom && other.Bottom > r.Top && other.Left > r.Right)
                .Select(other => other.Left)
                .DefaultIfEmpty(context.ImageSize.Width) // 右側に矩形がない場合は画像の幅を使用
                .Min();
            // 新しい幅を計算（左端からmaxRightまで、ただし元の幅より小さくならない）
            yield return rectList[i] with { MaxWidth = Math.Max(r.Width, maxRight - r.X) };
        }
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
        => texts;
}
