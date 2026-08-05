using Windows.Graphics.Imaging;

namespace WindowTranslator.Tests;

/// <summary>
/// 優先矩形を考慮した認識処理のテスト
/// </summary>
public class PriorityRectRecognizerTests
{
    private const int Width = 400;
    private const int Height = 300;

    private static SoftwareBitmap CreateBitmap()
        => new(BitmapPixelFormat.Bgra8, Width, Height, BitmapAlphaMode.Premultiplied);

    private static TextRect Text(string text, double x, double y, double width = 40, double height = 20)
        => new(text, x, y, width, height, height, false);

    [Fact]
    public async Task 優先矩形がない場合は全体の認識だけを行う()
    {
        using var bitmap = CreateBitmap();
        var calls = 0;

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, [], (target, source) =>
        {
            calls++;
            Assert.Same(bitmap, target);
            Assert.Same(bitmap, source);
            return ValueTask.FromResult<IEnumerable<TextRect>>([Text("full", 10, 10)]);
        });

        Assert.Equal(1, calls);
        Assert.Equal("full", Assert.Single(results).SourceText);
    }

    [Fact]
    public async Task 優先矩形があっても全体の認識を行う()
    {
        using var bitmap = CreateBitmap();
        // 画像の左上4分の1を優先矩形にする
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5)];

        // 優先矩形は左上、全体の結果は右下で重ならない
        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
            ValueTask.FromResult<IEnumerable<TextRect>>(
                ReferenceEquals(target, source) ? [Text("full", 300, 250)] : [Text("priority", 10, 10)]));

        Assert.Equal(["priority", "full"], results.Select(r => r.SourceText));
    }

    [Fact]
    public async Task 優先矩形の結果は全体画像の座標系に変換される()
    {
        using var bitmap = CreateBitmap();
        // 画像の右下4分の1を優先矩形にする
        PriorityRect[] rects = [new(0.5, 0.5, 0.5, 0.5)];

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
            ValueTask.FromResult<IEnumerable<TextRect>>(
                ReferenceEquals(target, source) ? [] : [Text("priority", 10, 20)]));

        var result = Assert.Single(results);
        Assert.Equal(Width * 0.5 + 10, result.X);
        Assert.Equal(Height * 0.5 + 20, result.Y);
    }

    [Fact]
    public async Task 優先矩形の結果と重なる全体の結果は破棄される()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5)];

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
            ValueTask.FromResult<IEnumerable<TextRect>>(ReferenceEquals(target, source)
                // 全体の結果のうち、ひとつは優先矩形の結果と同じ位置で重なる
                ? [Text("full-overlapped", 10, 10), Text("full", 300, 250)]
                : [Text("priority", 10, 10)]));

        Assert.Equal(["priority", "full"], results.Select(r => r.SourceText));
    }

    [Fact]
    public async Task 優先矩形で認識できなかった場合は全体の結果を残す()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5)];

        // 優先矩形の切り出し画像では何も認識できない状況
        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
            ValueTask.FromResult<IEnumerable<TextRect>>(
                ReferenceEquals(target, source) ? [Text("full", 10, 10)] : []));

        Assert.Equal("full", Assert.Single(results).SourceText);
    }

    [Fact]
    public async Task 優先度の高い矩形の結果と重なる結果は破棄される()
    {
        using var bitmap = CreateBitmap();
        // 同じ領域を指す2つの矩形を、優先度の高い順に登録する
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5, "high"), new(0, 0, 0.5, 0.5, "low")];

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
            ValueTask.FromResult<IEnumerable<TextRect>>(
                ReferenceEquals(target, source) ? [] : [Text("priority", 10, 10)]));

        // 優先度の低い矩形の結果は破棄され、キーワードは優先度の高い矩形のものになる
        Assert.Equal("high", Assert.Single(results).Context);
    }

    [Fact]
    public async Task 優先矩形のキーワードが翻訳のコンテキストになる()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5, "キーワード")];

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
            ValueTask.FromResult<IEnumerable<TextRect>>(
                ReferenceEquals(target, source) ? [] : [Text("priority", 10, 10)]));

        Assert.Equal("キーワード", Assert.Single(results).Context);
    }

    [Fact]
    public async Task 優先矩形の認識では基準として全体画像が渡される()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5)];

        await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
        {
            // 画像サイズを基準にした閾値を切り出し画像でも全体画像基準で計算できる必要がある
            Assert.Same(bitmap, source);
            Assert.Equal(Width, source.PixelWidth);
            Assert.Equal(Height, source.PixelHeight);
            return ValueTask.FromResult<IEnumerable<TextRect>>([]);
        });
    }

    [Fact]
    public async Task 切り出せない大きさの優先矩形は無視される()
    {
        using var bitmap = CreateBitmap();
        // 1ピクセル未満に潰れる矩形と、画像の外にある矩形
        PriorityRect[] rects = [new(0, 0, 0.001, 0.001), new(1.5, 1.5, 0.5, 0.5)];
        var calls = 0;

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
        {
            calls++;
            return ValueTask.FromResult<IEnumerable<TextRect>>([Text("full", 10, 10)]);
        });

        // 全体の認識のみが行われる
        Assert.Equal(1, calls);
        Assert.Equal("full", Assert.Single(results).SourceText);
    }
}
