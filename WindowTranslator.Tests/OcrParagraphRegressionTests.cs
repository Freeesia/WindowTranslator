using System.Drawing;
using System.Text.Json;
using WindowTranslator.Modules;

namespace WindowTranslator.Tests;

public class OcrParagraphRegressionTests
{
    private sealed record LineData(int Words, double X, double Y, double Width, double Height, double FontSize, double Angle, double TextHeight, int[]? Symbols);
    private sealed record Sample(string Name, int Width, int Height, LineData[] Lines, int[][] Paragraphs);
    private static readonly Sample[] Samples = LoadSamples();

    private static Sample[] LoadSamples()
    {
        using var stream = typeof(OcrParagraphRegressionTests).Assembly.GetManifestResourceStream("WindowTranslator.Tests.TestData.OcrParagraphs.json")!;
        return JsonSerializer.Deserialize<Sample[]>(stream)!;
    }

    public static IEnumerable<object[]> ImageTransforms()
    {
        foreach (var sample in Samples)
            foreach (var scale in new[] { .5, 1, 2 })
                foreach (var angle in new[] { -7d, 0, 7 })
                    yield return [sample.Name, scale, angle];
    }

    [Theory]
    [MemberData(nameof(ImageTransforms))]
    public void 実画像の本文19領域を倍率と傾きによらず読み順どおり結合する(string name, double scale, double rotation)
    {
        var sample = Samples.Single(s => s.Name == name);
        var radians = rotation * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var canvasWidth = sample.Width * Math.Abs(cos) + sample.Height * Math.Abs(sin);
        var canvasHeight = sample.Width * Math.Abs(sin) + sample.Height * Math.Abs(cos);
        var lines = sample.Lines.Select((r, i) =>
        {
            var x = r.X - sample.Width / 2d;
            var y = r.Y - sample.Height / 2d;
            // 原文の内容に依存しないよう、単語数を保った識別子に置き換える。
            var text = string.Join(" ", Enumerable.Range(0, r.Words).Select(word => r.Symbols?.Contains(word) == true ? "◇" : $"line{i}word{word}"));
            return new TextRect(text, (x * cos - y * sin + canvasWidth / 2) * scale,
                (x * sin + y * cos + canvasHeight / 2) * scale,
                r.Width * scale, r.Height * scale, r.FontSize * scale, false) { Angle = r.Angle + rotation };
        }).ToArray();
        var textHeights = new Dictionary<TextRect, double>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < lines.Length; i++)
            textHeights.Add(lines[i], sample.Lines[i].TextHeight * scale);

        var results = OcrUtility.MergeTextRects(lines, new Size((int)(canvasWidth * scale), (int)(canvasHeight * scale)), new(), "en-US", textHeights);

        foreach (var paragraph in sample.Paragraphs)
        {
            var expected = string.Join(" ", paragraph.Select(id => lines[id].SourceText));
            var result = Assert.Single(results, r => r.SourceText == expected);
            Assert.Equal(paragraph.Length > 1, result.MultiLine);
        }
    }

    [Fact]
    public void 数値フィールドを上の短い見出しと追加結合しない()
    {
        var source = new[] {
            new TextRect("GAME", 24.33362256, 4.98611714, 48.98936978, 16.01735772, 14.41562195, false) { Angle = 1.19348942 },
            new TextRect("EXP: 1000", 1, 35, 97, 19, 17.1, false),
        };
        Assert.Equal(["GAME", "EXP: 1000"], OcrUtility.MergeTextRects(source, new(1920, 1080), new(), "en-US").Select(r => r.SourceText));
    }

    [Fact]
    public void 数値を含まない短い二行の中央揃えは維持する()
    {
        var source = new[] {
            new TextRect("Select production", 1697, 53, 149, 19, 17.1, false),
            new TextRect("n BORNEO", 1727.79572254, 73.00219653, 93.20966545, 18.99890170, 17.09901153, false) { Angle = -.61605991 },
        };
        var result = Assert.Single(OcrUtility.MergeTextRects(source, new(1920, 1080), new(), "en-US"));
        Assert.Equal("Select production n BORNEO", result.SourceText);
    }

    [Fact]
    public void 短い末尾行の単語高を使い空行で次の段落を分ける()
    {
        var lines = new[] {
            new TextRect("first line of body", 100, 100, 300, 30, 27, false),
            new TextRect("last full body line", 100, 140, 300, 32, 28.8, false),
            new TextRect("rooms.", 100, 180, 60, 20, 18, false),
            new TextRect("next paragraph starts here", 100, 260, 300, 30, 27, false),
        };
        var heights = new Dictionary<TextRect, double>(ReferenceEqualityComparer.Instance) {
            [lines[0]] = 25, [lines[1]] = 25, [lines[2]] = 23, [lines[3]] = 25,
        };
        var results = OcrUtility.MergeTextRects(lines, new(1920, 1080), new(), "en-US", heights);
        Assert.Equal(["first line of body last full body line rooms.", "next paragraph starts here"], results.Select(r => r.SourceText));
    }

    [Fact]
    public void 本文に挟まれた大きな見出しの文字サイズを本文と同一視しない()
    {
        var lines = new[] {
            new TextRect("body text before heading", 100, 100, 300, 22, 20, false),
            new TextRect("Important Chapter Heading", 100, 140, 300, 35, 32, false),
            new TextRect("body text after heading", 100, 180, 300, 22, 20, false),
        };
        var results = OcrUtility.MergeTextRects(lines, new(1920, 1080), new(), "en-US");
        Assert.Equal(lines.Select(r => r.SourceText), results.Select(r => r.SourceText));
    }

    [Fact]
    public void 角度が180度をまたぐ折り返しも読み順を保つ()
    {
        var lines = new[] {
            new TextRect("first line of paragraph", -500, -100, 300, 27, 24.3, false) { Angle = 179 },
            new TextRect("second line of paragraph", -555, -145, 245, 27, 24.3, false) { Angle = -179 },
            new TextRect("third line of paragraph", -555, -190, 245, 27, 24.3, false) { Angle = 179 },
            new TextRect("last line of paragraph", -555, -235, 245, 27, 24.3, false) { Angle = -179 },
        };
        var result = Assert.Single(OcrUtility.MergeTextRects(lines, new(1920, 1080), new(), "en-US"));
        Assert.Equal(string.Join(" ", lines.Select(r => r.SourceText)), result.SourceText);
    }
}
