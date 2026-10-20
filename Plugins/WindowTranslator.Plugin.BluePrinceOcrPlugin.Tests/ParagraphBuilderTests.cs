using Xunit;

namespace WindowTranslator.Plugin.BluePrinceOcrPlugin;

public class ParagraphBuilderTests
{
    private static TextRect Line(string text, double x, double y, double angle = -3)
    {
        var a = angle * Math.PI / 180;
        return new(text, x * Math.Cos(a) - y * Math.Sin(a),
            x * Math.Sin(a) + y * Math.Cos(a), 600, 27, 24, false) { Angle = angle };
    }

    [Fact]
    public void 傾いた本文を空行で段落に分けて読み順を保持する()
    {
        var lines = new[]
        {
            Line("second paragraph", 500, 600),
            Line("continued", 500, 445),
            Line("first paragraph", 500, 400),
            Line("last line", 500, 490),
            Line("continued again", 500, 645),
        };
        var result = ParagraphBuilder.Build(lines);
        Assert.Equal(2, result.Count);
        Assert.Equal("first paragraph continued last line", result[0].SourceText);
        Assert.Equal("second paragraph continued again", result[1].SourceText);
        Assert.All(result, r => Assert.True(r.MultiLine));
        Assert.Equal(-3, result[0].Angle);
        Assert.InRange(result[0].Height, 116.9, 117.1);
    }

    [Fact]
    public void 別の列と異なる向きの文字を本文へ混ぜない()
    {
        var result = ParagraphBuilder.Build([
            Line("main text", 500, 400, 0),
            Line("side text", 1300, 440, 0),
            Line("rotated text", 500, 445, 15),
            Line("next line", 500, 445, 0),
        ]);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.SourceText == "main text next line");
    }

    [Fact]
    public void 不正な矩形と空文字を無視する()
    {
        Assert.Empty(ParagraphBuilder.Build([
            new("", 0, 0, 20, 20, 20, false),
            new("invalid", 0, 0, 0, 20, 20, false),
        ]));
    }
}
