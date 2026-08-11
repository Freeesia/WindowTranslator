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
    public async Task 優先矩形がある場合は指定範囲だけを認識する()
    {
        using var bitmap = CreateBitmap();
        // 画像の左上4分の1を優先矩形にする
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5)];
        var calls = 0;

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
        {
            calls++;
            Assert.NotSame(bitmap, target);
            Assert.Same(bitmap, source);
            return ValueTask.FromResult<IEnumerable<TextRect>>([Text("priority", 10, 10)]);
        });

        Assert.Equal(1, calls);
        Assert.Equal("priority", Assert.Single(results).SourceText);
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
    public async Task スケールを戻した回転結果へ切り出し位置をオフセットする()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0.25, 0.5, 0.5, 0.5, "context")];
        const double scale = 2;

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
        {
            Assert.Equal(Width / 2, target.PixelWidth);
            Assert.Equal(Height / 2, target.PixelHeight);
            Assert.Same(bitmap, source);

            // 実際のOCRモジュールと同じ共通変換で、スケール後の座標を切り出し画像の座標系へ戻す
            var scaled = Text("scaled", 20, 40, 80, 40) with { Angle = 30 };
            return ValueTask.FromResult<IEnumerable<TextRect>>([scaled.RestoreScale(scale)]);
        });

        var result = Assert.Single(results);
        Assert.Equal((Width * 0.25) + 10, result.X);
        Assert.Equal((Height * 0.5) + 20, result.Y);
        Assert.Equal(40, result.Width);
        Assert.Equal(20, result.Height);
        Assert.Equal(30, result.Angle);
        Assert.Equal("context", result.Context);
    }

    [Fact]
    public async Task 優先矩形で認識できなかった場合も全体の認識は行わない()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5)];
        var calls = 0;

        // 優先矩形の切り出し画像では何も認識できない状況
        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
        {
            calls++;
            Assert.NotSame(bitmap, target);
            return ValueTask.FromResult<IEnumerable<TextRect>>([]);
        });

        Assert.Equal(1, calls);
        Assert.Empty(results);
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
    public async Task 矩形同士が重なっていても認識結果が重ならなければ両方を保持する()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects =
        [
            new(0, 0, 0.5, 0.5, "high"),
            new(0.25, 0, 0.5, 0.5, "low"),
        ];
        var calls = 0;

        var results = (await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, (target, source) =>
        {
            calls++;
            return ValueTask.FromResult<IEnumerable<TextRect>>(calls == 1
                ? [Text("high text", 10, 10)]
                : [Text("low text", 80, 10)]);
        })).ToArray();

        Assert.Equal(["high text", "low text"], results.Select(r => r.SourceText));
        Assert.Equal(["high", "low"], results.Select(r => r.Context));
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

        Assert.Equal(0, calls);
        Assert.Empty(results);
    }

    [Fact]
    public async Task 指定範囲の切り出しは本体のキャプチャ形式であるBgra8だけを受け付ける()
    {
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Gray8, Width, Height, BitmapAlphaMode.Ignore);
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5)];

        await Assert.ThrowsAsync<ArgumentException>(() => PriorityRectRecognizer.RecognizeAsync(
            bitmap,
            rects,
            (target, source) => ValueTask.FromResult<IEnumerable<TextRect>>([])).AsTask());
    }
}
