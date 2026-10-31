using Windows.Graphics.Imaging;
using WindowTranslator.Modules;

namespace WindowTranslator.Tests;

/// <summary>
/// OCR対象範囲の認識処理に関するテスト
/// </summary>
public class OcrUtilityTests
{
    private static TextRect Line(string text, double x, double y, double angle = 0, double height = 27, double width = 600)
    {
        var radians = angle * Math.PI / 180;
        return new(text, x * Math.Cos(radians) - y * Math.Sin(radians),
            x * Math.Sin(radians) + y * Math.Cos(radians), width, height, height * .9, false) { Angle = angle };
    }

    private static IReadOnlyList<TextRect> Merge(IEnumerable<TextRect> lines, BasicOcrParam? options = null, string language = "en-US")
        => OcrUtility.MergeTextRects(lines, new(1920, 1080), options ?? new(), language);

    [Theory]
    [InlineData(-90)]
    [InlineData(-5)]
    [InlineData(-3)]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(45)]
    [InlineData(90)]
    public void 傾きによらず空行で段落を分け読み順と行の高さを保つ(double angle)
    {
        var results = Merge([
            Line("second paragraph", 500, 600, angle),
            Line("continued", 500, 445, angle),
            Line("first paragraph", 500, 400, angle),
            Line("last line", 500, 490, angle),
            Line("continued again", 500, 645, angle),
        ]);

        Assert.Equal(["first paragraph continued last line", "second paragraph continued again"], results.Select(r => r.SourceText));
        Assert.All(results, r => Assert.True(r.MultiLine));
        Assert.Equal(angle, results[0].Angle);
        Assert.Equal(117, results[0].Height, 6);
        Assert.Equal(24.3, results[0].FontSize, 6);
    }

    [Theory]
    [InlineData(.5)]
    [InlineData(1)]
    [InlineData(2)]
    public void 拡大率が変わっても段落境界と許容倍率の効果を保つ(double scale)
    {
        var lines = new[] { 100, 128, 156, 206, 234 }.Select((y, i) => Line($"line{i}", 500, y, -2))
            .Select(r => r with { X = r.X * scale, Y = r.Y * scale, Width = r.Width * scale, Height = r.Height * scale, FontSize = r.FontSize * scale })
            .ToArray();

        var imageSize = new System.Drawing.Size((int)(1920 * scale), (int)(1080 * scale));
        Assert.Equal(2, OcrUtility.MergeTextRects(lines, imageSize, new(), "en-US").Count);
        Assert.Single(OcrUtility.MergeTextRects(lines, imageSize, new() { LineSpacingTolerance = 2 }, "en-US"));
    }

    [Fact]
    public void 一定の広い行送りは同じ段落にまとめる()
    {
        var result = Assert.Single(Merge([
            Line("first", 500, 100), Line("second", 500, 165), Line("third", 500, 230),
        ]));
        Assert.Equal("first second third", result.SourceText);
    }

    [Fact]
    public void 行ごとの角度が少し揺れても外接矩形の上端を行送りに使わない()
    {
        var results = Merge([
            Line("first", 500, 100), Line("second", 500, 128) with { Angle = -2 },
            Line("third", 500, 156) with { Angle = 2 }, Line("next", 500, 206),
            Line("last", 500, 234) with { Angle = -2 },
        ]);
        Assert.Equal(["first second third", "next last"], results.Select(r => r.SourceText));
    }

    [Fact]
    public void 短い先頭行の角度が本文と異なっても本文の段落を分断しない()
    {
        var lines = new List<TextRect> { Line("caption", 500, 100, -.5, width: 110) };
        lines.AddRange(new[] { 220, 265, 310, 390, 435 }.Select((y, i) => Line($"line{i}", 500, y, -3)));

        var results = Merge(lines);
        Assert.Equal(["caption", "line0 line1 line2", "line3 line4"], results.Select(r => r.SourceText));
    }

    [Fact]
    public void 行末の小さな文字は直前の一行だけでなく段落の文字サイズと比較する()
    {
        var result = Assert.Single(Merge([
            Line("first", 500, 100, height: 28), Line("second", 500, 145, height: 29),
            Line("third", 500, 190, height: 32), Line("end", 500, 235, height: 25, width: 180),
        ]));
        Assert.Equal("first second third end", result.SourceText);
    }

    [Fact]
    public void 字下げされた折り返しを無視して別々のリスト項目を結合しない()
    {
        var results = Merge([
            Line("first item", 500, 100, height: 34), Line("continuation", 555, 136, height: 26, width: 410),
            Line("second item", 500, 197, height: 31), Line("another continuation", 555, 234, height: 26, width: 400),
            Line("third item", 500, 294, height: 31),
        ]);
        Assert.Equal(5, results.Count);
        Assert.Equal(["first item", "continuation", "second item", "another continuation", "third item"], results.Select(r => r.SourceText));
    }

    [Fact]
    public void 二行の距離だけを通常の行送りとみなして離れた段落を結合しない()
    {
        Assert.Equal(2, Merge([Line("first", 500, 100), Line("second", 500, 180)]).Count);
        Assert.Single(Merge([Line("first", 500, 100), Line("second", 500, 145)]));
        Assert.Equal(2, Merge([Line("first", 500, 100), Line("second", 500, 145)], new() { LeadingThrethold = .5 }).Count);
    }

    [Fact]
    public void 単語を横に結合してから段落の行送りを推定する()
    {
        var result = Assert.Single(Merge([
            Line("world", 610, 100, 2, width: 100), Line("hello", 500, 100, 2, width: 100),
            Line("second line", 500, 145, 2, width: 210), Line("third line", 500, 190, 2, width: 210),
        ]));
        Assert.Equal("hello world second line third line", result.SourceText);
        Assert.True(result.MultiLine);
    }

    [Fact]
    public void 横に結合しただけでは複数行にしない()
    {
        var result = Assert.Single(Merge([Line("hello", 500, 100, width: 100), Line("world", 610, 100, width: 100)]));
        Assert.Equal("hello world", result.SourceText);
        Assert.False(result.MultiLine);
    }

    [Fact]
    public void 中央揃えの行も段落にまとめる()
    {
        var result = Assert.Single(Merge([
            Line("long first line", 500, 100, -2, width: 300),
            Line("short", 600, 145, -2, width: 100),
            Line("third line", 550, 190, -2, width: 200),
        ]));
        Assert.Equal("long first line short third line", result.SourceText);
    }

    [Fact]
    public void リストの結合回避を維持する()
    {
        var lines = new[] { Line("Open", 500, 100), Line("Save", 500, 128), Line("Close", 500, 156) };
        Assert.Single(Merge(lines));
        Assert.Equal(3, Merge(lines, new() { IsAvoidMergeList = true }).Count);
    }

    [Fact]
    public void 左端だけが揃う短い見出しと大幅に広い本文を結合しない()
    {
        Assert.Equal(2, Merge([Line("caption", 500, 100, width: 100), Line("long body text", 500, 140)]).Count);
    }

    [Fact]
    public void 別の列と異なる向きの行を混ぜない()
    {
        var results = Merge([
            Line("main text", 500, 400), Line("side text", 1300, 445),
            Line("rotated text", 500, 445, 15), Line("next line", 500, 445),
        ]);
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.SourceText == "main text next line");
    }

    [Fact]
    public void 本番と同じ文字サイズでアイコン入り行と大小の見出しを許容率に従って結合する()
    {
        var source = new[] {
            Line("title", 700, 160, height: 41), Line("large title", 700, 220, height: 81),
            Line("first paragraph", 700, 340, height: 29), Line("icon line", 700, 395, height: 43),
            Line("second paragraph", 700, 510, height: 32), Line("another icon", 700, 550, height: 63),
            Line("continued", 700, 625, height: 29), Line("continued again", 700, 680, height: 29), Line("end", 700, 740, height: 29),
        };

        var results = Merge(source, new() { LineSpacingTolerance = 1.9, FontSizeThrethold = 1.2 });
        Assert.Equal(["title large title", "first paragraph icon line", "second paragraph another icon continued continued again end"], results.Select(r => r.SourceText));
        Assert.True(Merge(source).Count > results.Count);
    }

    [Fact]
    public void 他のOCRの文字サイズの尺度でも行送りは矩形高から推定する()
    {
        var source = new[] { 100, 128, 156, 206, 234 }.Select((y, i) => Line($"line{i}", 500, y) with { FontSize = 12 });
        Assert.Equal(2, Merge(source).Count);
    }

    [Fact]
    public void 空白で区切らない言語では余分な空白を追加しない()
    {
        var result = Assert.Single(Merge([Line("こんにちは", 500, 100), Line("世界", 500, 145)], language: "ja-JP"));
        Assert.Equal("こんにちは世界", result.SourceText);
    }

    [Fact]
    public void 単独の既存の複数行矩形とコンテキストを維持する()
    {
        var source = Line("existing paragraph", 500, 100, 2) with { MultiLine = true, Context = "context" };
        Assert.Same(source, Assert.Single(Merge([source])));
        Assert.Empty(Merge([TextRect.Empty, Line(" ", 500, 100)]));
    }

    [Fact]
    public void 千行の独立した行を段落ごとの行送り再推定なしで処理する()
    {
        var source = Enumerable.Range(0, 1000).Select(i => Line($"line{i}", 500, 150 * i));
        Assert.Equal(1000, Merge(source).Count);
    }

    [Fact]
    public async Task 共通結合は別のOCR対象範囲とコンテキストを混ぜない()
    {
        using var bitmap = CreateBitmap();
        var input = new OcrCaptureInput(bitmap, [
            new(new(0, 0, 200, 150), "first region"),
            new(new(200, 0, 200, 150), "second region"),
        ]);

        var results = await OcrUtility.RecognizeRegionsAsync(input, (_, size) =>
            ValueTask.FromResult(OcrUtility.MergeTextRects([Text("first", 10, 10), Text("second", 10, 40)], size, new(), "en-US")));

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("first second", r.SourceText));
        Assert.Equal(["first region", "second region"], results.Select(r => r.Context));
        Assert.Equal([10d, 210d], results.Select(r => r.X));
    }

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

        var results = await OcrUtility.RecognizeRegionsAsync(input, (target, _) =>
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

        var results = await OcrUtility.RecognizeRegionsAsync(input, (target, _) =>
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
            (target, sourceSize) =>
            {
                Assert.Equal(200, target.PixelWidth);
                Assert.Equal(150, target.PixelHeight);
                Assert.Equal(new System.Drawing.Size(800, 600), sourceSize);
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
            (target, _) =>
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
            (_, _) =>
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
            (_, _) => ValueTask.FromResult(regionResults.Dequeue()));

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
            (_, _) => ValueTask.FromResult<IReadOnlyList<TextRect>>([
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
            (_, _) => ValueTask.FromResult(regionResults.Dequeue()));

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
            (_, _) => ValueTask.FromResult(regionResults.Dequeue()));

        Assert.Equal(["high text", "low text"], results.Select(r => r.SourceText));
    }
}
