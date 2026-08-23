using Windows.Graphics.Imaging;
using WindowTranslator.Modules;

namespace WindowTranslator.Tests;

/// <summary>
/// OCR対象範囲の認識処理に関するテスト
/// </summary>
public class OcrUtilityTests
{
    private const int Width = 400;
    private const int Height = 300;

    private static SoftwareBitmap CreateBitmap()
        => new(BitmapPixelFormat.Bgra8, Width, Height, BitmapAlphaMode.Premultiplied);

    private static TextRect Text(string text, double x, double y, double width = 40, double height = 20)
        => new(text, x, y, width, height, height, false);

    [Fact]
    public async Task 全体範囲は元画像をそのまま認識してコンテキストを維持する()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [new(new(0, 0, Width, Height))]);

        var results = await OcrUtility.RecognizeRegionsAsync(input, target =>
        {
            Assert.Same(bitmap, target);
            return ValueTask.FromResult<IReadOnlyList<TextRect>>([
                Text("full", 10, 10) with { Context = "module context" },
            ]);
        });

        Assert.Equal("module context", Assert.Single(results).Context);
    }

    [Fact]
    public async Task 指定範囲を切り出して全体画像座標とコンテキストへ変換する()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [new(new(101, 150, 101, 77), "context")]);

        var results = await OcrUtility.RecognizeRegionsAsync(input, target =>
        {
            Assert.NotSame(bitmap, target);
            Assert.Equal(101, target.PixelWidth);
            Assert.Equal(77, target.PixelHeight);
            return ValueTask.FromResult<IReadOnlyList<TextRect>>([Text("region", 10, 20)]);
        });

        var result = Assert.Single(results);
        Assert.Equal(111, result.X);
        Assert.Equal(170, result.Y);
        Assert.Equal("context", result.Context);
    }

    [Fact]
    public async Task 拡大画像の認識結果を元画像座標へ戻す()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [new(new(100, 150, 100, 75), "context")]);

        var results = await OcrUtility.RecognizeRegionsAsync(
            input,
            target =>
            {
                Assert.Equal(200, target.PixelWidth);
                Assert.Equal(150, target.PixelHeight);
                return ValueTask.FromResult<IReadOnlyList<TextRect>>([
                    Text("scaled", 20, 40, 80, 40) with { Angle = 30 },
                ]);
            },
            scale: 2);

        var result = Assert.Single(results);
        Assert.Equal(110, result.X);
        Assert.Equal(170, result.Y);
        Assert.Equal(40, result.Width);
        Assert.Equal(20, result.Height);
        Assert.Equal(20, result.FontSize);
        Assert.Equal(30, result.Angle);
        Assert.Equal("context", result.Context);
    }

    [Fact]
    public async Task 画像補正時は元画像を変更しない()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [new(new(0, 0, Width, Height))]);

        await OcrUtility.RecognizeRegionsAsync(
            input,
            target =>
            {
                Assert.NotSame(bitmap, target);
                return ValueTask.FromResult<IReadOnlyList<TextRect>>([]);
            },
            brightness: 1);
    }

    [Fact]
    public async Task 対象範囲が空の場合は認識しない()
    {
        using var bitmap = CreateBitmap();
        var calls = 0;

        var results = await OcrUtility.RecognizeRegionsAsync(
            new(bitmap, []),
            _ =>
            {
                calls++;
                return ValueTask.FromResult<IReadOnlyList<TextRect>>([]);
            });

        Assert.Equal(0, calls);
        Assert.Empty(results);
    }

    [Fact]
    public async Task 高優先度の結果と50パーセント重なる低優先度の結果を破棄する()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [
            new(new(0, 0, 200, 150), "high"),
            new(new(0, 0, 200, 150), "low"),
        ]);
        var regionResults = new Queue<IReadOnlyList<TextRect>>([
            [Text("high text", 10, 10)],
            [Text("low text", 30, 10)],
        ]);

        var results = await OcrUtility.RecognizeRegionsAsync(
            input,
            _ => ValueTask.FromResult(regionResults.Dequeue()));

        var result = Assert.Single(results);
        Assert.Equal("high text", result.SourceText);
        Assert.Equal("high", result.Context);
    }

    [Fact]
    public async Task 同じ対象範囲内で重なる認識結果は両方を保持する()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [new(new(0, 0, 200, 150), "context")]);

        var results = await OcrUtility.RecognizeRegionsAsync(
            input,
            _ => ValueTask.FromResult<IReadOnlyList<TextRect>>([
                Text("first", 10, 10),
                Text("second", 20, 10),
            ]));

        Assert.Equal(["first", "second"], results.Select(r => r.SourceText));
    }

    [Fact]
    public async Task 高優先度の結果との重なりが50パーセント未満なら両方を保持する()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [
            new(new(0, 0, 200, 150), "high"),
            new(new(0, 0, 200, 150), "low"),
        ]);
        var regionResults = new Queue<IReadOnlyList<TextRect>>([
            [Text("high text", 10, 10)],
            [Text("low text", 40, 10)],
        ]);

        var results = await OcrUtility.RecognizeRegionsAsync(
            input,
            _ => ValueTask.FromResult(regionResults.Dequeue()));

        Assert.Equal(["high text", "low text"], results.Select(r => r.SourceText));
        Assert.Equal(["high", "low"], results.Select(r => r.Context));
    }

    [Fact]
    public async Task 対象範囲が重なっても認識結果が重ならなければ両方を保持する()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [
            new(new(0, 0, 200, 150), "high"),
            new(new(100, 0, 200, 150), "low"),
        ]);
        var regionResults = new Queue<IReadOnlyList<TextRect>>([
            [Text("high text", 10, 10)],
            [Text("low text", 80, 10)],
        ]);

        var results = await OcrUtility.RecognizeRegionsAsync(
            input,
            _ => ValueTask.FromResult(regionResults.Dequeue()));

        Assert.Equal(["high text", "low text"], results.Select(r => r.SourceText));
    }
}
