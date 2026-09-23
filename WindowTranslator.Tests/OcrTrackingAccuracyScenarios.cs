using System.Drawing;

namespace WindowTranslator.Tests;

internal static class OcrTrackingAccuracyScenarios
{
    public static IReadOnlyList<Scenario> All { get; } =
    [
        new("static jitter and transient typo",
        [
            [Rect("Start Game", 100, 100, 200, 40)],
            [Rect("Start Game", 102, 99, 198, 41)],
            [Rect("Start Garne", 99, 101, 203, 39)],
            [],
            [Rect("Start Game", 101, 100, 201, 40)],
        ],
        [
            [Rect("Start Game", 100, 100, 200, 40)],
            [Rect("Start Game", 100, 100, 200, 40)],
            [Rect("Start Game", 100, 100, 200, 40)],
            [Rect("Start Game", 100, 100, 200, 40)],
            [Rect("Start Game", 100, 100, 200, 40)],
        ]),
        new("one-to-one assignment",
        [
            [Rect("Score", 100, 200, 80, 30), Rect("Score", 125, 200, 80, 30)],
            [Rect("Score", 113, 200, 80, 30), Rect("Score", 111, 200, 80, 30)],
        ],
        [
            [Rect("Score", 100, 200, 80, 30), Rect("Score", 125, 200, 80, 30)],
            [Rect("Score", 100, 200, 80, 30), Rect("Score", 125, 200, 80, 30)],
        ]),
        new("temporary split",
        [
            [Rect("New Game", 300, 300, 180, 36)],
            [Rect("New", 300, 300, 76, 36), Rect("Game", 382, 300, 98, 36)],
            [Rect("New Game", 301, 300, 179, 36)],
        ],
        [
            [Rect("New Game", 300, 300, 180, 36)],
            [Rect("New Game", 300, 300, 180, 36)],
            [Rect("New Game", 300, 300, 180, 36)],
        ]),
        new("temporary merge",
        [
            [Rect("New", 50, 400, 70, 32), Rect("Game", 125, 400, 90, 32)],
            [Rect("New Game", 50, 400, 165, 32)],
            [Rect("New", 50, 400, 70, 32), Rect("Game", 125, 400, 90, 32)],
        ],
        [
            [Rect("New", 50, 400, 70, 32), Rect("Game", 125, 400, 90, 32)],
            [Rect("New", 50, 400, 70, 32), Rect("Game", 125, 400, 90, 32)],
            [Rect("New", 50, 400, 70, 32), Rect("Game", 125, 400, 90, 32)],
        ]),
        new("transient and sustained text change",
        [
            [Rect("Pause", 600, 100, 120, 34)],
            [Rect("Resume", 600, 100, 120, 34)],
            [Rect("Pause", 600, 100, 120, 34)],
            [Rect("Resume", 600, 100, 120, 34)],
            [Rect("Resume", 600, 100, 120, 34)],
        ],
        [
            [Rect("Pause", 600, 100, 120, 34)],
            [Rect("Pause", 600, 100, 120, 34)],
            [Rect("Pause", 600, 100, 120, 34)],
            [Rect("Pause", 600, 100, 120, 34)],
            [Rect("Resume", 600, 100, 120, 34)],
        ]),
        new("real movement",
        [
            [Rect("Moving", 100, 500, 130, 30)],
            [Rect("Moving", 102, 501, 130, 30)],
            [Rect("Moving", 350, 500, 130, 30)],
            [Rect("Moving", 352, 500, 130, 30)],
        ],
        [
            [Rect("Moving", 100, 500, 130, 30)],
            [Rect("Moving", 100, 500, 130, 30)],
            [Rect("Moving", 350, 500, 130, 30)],
            [Rect("Moving", 350, 500, 130, 30)],
        ]),
    ];

    public static double Measure(IReadOnlyList<IReadOnlyList<TextRect>> actualFrames)
    {
        var expectedFrames = All.SelectMany(s => s.Expected).ToArray();
        var actual = actualFrames.ToArray();
        Assert.Equal(expectedFrames.Length, actual.Length);

        return expectedFrames.Zip(actual, ScoreFrame).Average();
    }

    private static double ScoreFrame(IReadOnlyList<TextRect> expected, IReadOnlyList<TextRect> actual)
    {
        if (expected.Count == 0)
        {
            return actual.Count == 0 ? 1 : 0;
        }

        var remaining = actual.ToList();
        var score = 0d;
        foreach (var expectedRect in expected)
        {
            var best = remaining
                .Select((rect, index) => (rect, index, score: ScoreRect(expectedRect, rect)))
                .OrderByDescending(candidate => candidate.score)
                .FirstOrDefault();
            if (best.rect is not null)
            {
                score += best.score;
                remaining.RemoveAt(best.index);
            }
        }

        return score / Math.Max(expected.Count, actual.Count);
    }

    private static double ScoreRect(TextRect expected, TextRect actual)
    {
        var text = expected.SourceText == actual.SourceText ? 0.5 : 0;
        var geometry = IntersectionOverUnion(expected, actual) * 0.5;
        return text + geometry;
    }

    private static double IntersectionOverUnion(TextRect first, TextRect second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.X + first.Width, second.X + second.Width);
        var bottom = Math.Min(first.Y + first.Height, second.Y + second.Height);
        var intersection = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        var union = first.Width * first.Height + second.Width * second.Height - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static TextRect Rect(string text, double x, double y, double width, double height)
        => new(text, x, y, width, height, height * 0.8, false, Color.White, Color.Black);
}

internal sealed record Scenario(
    string Name,
    IReadOnlyList<IReadOnlyList<TextRect>> Observations,
    IReadOnlyList<IReadOnlyList<TextRect>> Expected);
