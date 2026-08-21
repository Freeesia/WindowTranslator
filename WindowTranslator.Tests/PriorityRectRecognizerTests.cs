using Windows.Graphics.Imaging;
using WindowTranslator.Modules;

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

    private static ValueTask<IReadOnlyList<IReadOnlyList<TextRect>>> RecognizeRegionsAsync(
        OcrCaptureInput input,
        Func<SoftwareBitmap, IReadOnlyList<TextRect>> recognize)
        => OcrUtility.RecognizeRegionsAsync(
            input,
            bitmap => ValueTask.FromResult(recognize(bitmap)));

    [Fact]
    public async Task 優先矩形がない場合は全体の認識だけを行う()
    {
        using var bitmap = CreateBitmap();
        var calls = 0;

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, [], input =>
        {
            calls++;
            Assert.Same(bitmap, input.Source);
            Assert.Single(input.Regions);
            return RecognizeRegionsAsync(input, target =>
            {
                Assert.Same(bitmap, target);
                return [Text("full", 10, 10)];
            });
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

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
        {
            calls++;
            Assert.Same(bitmap, input.Source);
            Assert.Single(input.Regions);
            return RecognizeRegionsAsync(input, target =>
            {
                Assert.NotSame(bitmap, target);
                return [Text("priority", 10, 10)];
            });
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

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
            RecognizeRegionsAsync(input, _ => [Text("priority", 10, 20)]));

        var result = Assert.Single(results);
        Assert.Equal(Width * 0.5 + 10, result.X);
        Assert.Equal(Height * 0.5 + 20, result.Y);
    }

    [Fact]
    public async Task 小数座標の優先矩形は実際の切り出し位置から全体画像座標へ戻される()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0.253, 0.502, 0.251, 0.252)];

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
        {
            // (101.2, 150.6) - (201.6, 226.2) と交差する全ピクセルを切り出す
            Assert.Same(bitmap, input.Source);
            return RecognizeRegionsAsync(input, target =>
            {
                Assert.Equal(101, target.PixelWidth);
                Assert.Equal(77, target.PixelHeight);
                return [Text("fractional", 0, 0)];
            });
        });

        var result = Assert.Single(results);
        Assert.Equal(101, result.X);
        Assert.Equal(150, result.Y);
    }

    [Fact]
    public async Task スケールを戻した回転結果へ切り出し位置をオフセットする()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0.25, 0.5, 0.5, 0.5, "context")];
        const double scale = 2;

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
        {
            Assert.Same(bitmap, input.Source);

            // 実際のOCRモジュールと同じ共通変換で、スケール後の座標を切り出し画像の座標系へ戻す
            var scaled = Text("scaled", 20, 40, 80, 40) with { Angle = 30 };
            return RecognizeRegionsAsync(input, target =>
            {
                Assert.Equal(Width / 2, target.PixelWidth);
                Assert.Equal(Height / 2, target.PixelHeight);
                return [scaled.RestoreScale(scale)];
            });
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

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
        {
            calls++;
            return RecognizeRegionsAsync(input, target =>
            {
                Assert.NotSame(bitmap, target);
                return [];
            });
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

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
            RecognizeRegionsAsync(input, _ => [Text("priority", 10, 10)]));

        // 優先度の低い矩形の結果は破棄され、キーワードは優先度の高い矩形のものになる
        Assert.Equal("high", Assert.Single(results).Context);
    }

    [Fact]
    public async Task 低優先度側の認識結果との重なりが50パーセント未満なら両方を保持する()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5, "high"), new(0, 0, 0.5, 0.5, "low")];
        var regionResults = new Queue<IReadOnlyList<TextRect>>([
            [Text("high text", 10, 10)],
            [Text("low text", 40, 10)],
        ]);

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
            RecognizeRegionsAsync(input, _ => regionResults.Dequeue()));

        Assert.Equal(["high text", "low text"], results.Select(r => r.SourceText));
    }

    [Fact]
    public async Task 低優先度側の認識結果との重なりが50パーセントなら破棄する()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5, "high"), new(0, 0, 0.5, 0.5, "low")];
        var regionResults = new Queue<IReadOnlyList<TextRect>>([
            [Text("high text", 10, 10)],
            [Text("low text", 30, 10)],
        ]);

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
            RecognizeRegionsAsync(input, _ => regionResults.Dequeue()));

        Assert.Equal("high text", Assert.Single(results).SourceText);
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
        var regionResults = new Queue<IReadOnlyList<TextRect>>([
            [Text("high text", 10, 10)],
            [Text("low text", 80, 10)],
        ]);

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
            RecognizeRegionsAsync(input, _ => regionResults.Dequeue()));

        Assert.Equal(["high text", "low text"], results.Select(r => r.SourceText));
        Assert.Equal(["high", "low"], results.Select(r => r.Context));
    }

    [Fact]
    public async Task 優先矩形のキーワードが翻訳のコンテキストになる()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(0, 0, 0.5, 0.5, "キーワード")];

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
            RecognizeRegionsAsync(input, _ => [Text("priority", 10, 10)]));

        Assert.Equal("キーワード", Assert.Single(results).Context);
    }

    [Fact]
    public async Task 複数の優先矩形は1回のキャプチャーとしてまとめて渡される()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects =
        [
            new(0, 0, 0.5, 0.5),
            new(0.5, 0.5, 0.25, 0.25),
        ];
        var calls = 0;

        await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
        {
            calls++;
            Assert.Same(bitmap, input.Source);
            Assert.Equal(Width, input.Source.PixelWidth);
            Assert.Equal(Height, input.Source.PixelHeight);
            Assert.Collection(
                input.Regions,
                region =>
                {
                    Assert.Equal(new(0, 0, Width / 2, Height / 2), region.Bounds);
                },
                region =>
                {
                    Assert.Equal(new(Width / 2, Height / 2, Width / 4, Height / 4), region.Bounds);
                });
            return RecognizeRegionsAsync(input, _ => []);
        });

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task 切り出せない大きさの優先矩形は無視される()
    {
        using var bitmap = CreateBitmap();
        // 1ピクセル未満に潰れる矩形と、画像の外にある矩形
        PriorityRect[] rects = [new(0, 0, 0.001, 0.001), new(1.5, 1.5, 0.5, 0.5)];
        var calls = 0;

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
        {
            calls++;
            return RecognizeRegionsAsync(input, _ => [Text("full", 10, 10)]);
        });

        Assert.Equal(0, calls);
        Assert.Empty(results);
    }

    [Fact]
    public async Task 画像外にはみ出した優先矩形は画像内へ切り詰められる()
    {
        using var bitmap = CreateBitmap();
        PriorityRect[] rects = [new(-0.1, -0.1, 0.2, 0.2)];

        var results = await PriorityRectRecognizer.RecognizeAsync(bitmap, rects, input =>
        {
            Assert.Same(bitmap, input.Source);
            return RecognizeRegionsAsync(input, target =>
            {
                Assert.Equal(40, target.PixelWidth);
                Assert.Equal(30, target.PixelHeight);
                return [Text("clamped", 0, 0)];
            });
        });

        var result = Assert.Single(results);
        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
    }
}
