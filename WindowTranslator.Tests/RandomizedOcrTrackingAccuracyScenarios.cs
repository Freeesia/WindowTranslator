using System.Drawing;

namespace WindowTranslator.Tests;

internal static class RandomizedOcrTrackingAccuracyScenarios
{
    public static IReadOnlyList<int> Seeds { get; } = [643, 0x5EED, 20260719];

    public static RandomizedScenario Create(int seed)
    {
        Size imageSize = new(1920, 1080);
        const int frameCount = 96;
        Entity[] entities =
        [
            new("long-menu", "Alpha Beta Gamma Delta Epsilon", null, int.MaxValue, 80, 80, 360, 34, 0, 0, 0, false),
            new("new", "New", null, int.MaxValue, 80, 190, 70, 32, 0, 0, 0, false),
            new("game", "Game", null, int.MaxValue, 155, 190, 90, 32, 0, 0, 0, false),
            new("score-left", "Score", null, int.MaxValue, 300, 300, 100, 30, 3, 0, 0, false),
            new("score-right", "Score", null, int.MaxValue, 600, 300, 100, 30, -3, 0, 0, false),
            new("inventory", "Inventory", null, int.MaxValue, 80, 430, 150, 34, 0, 0, 0, false),
            new("quest", "Quest Log", null, int.MaxValue, 300, 430, 150, 34, 0.4, 0.15, 359, false),
            new("hp", "HP 100", "HP 075", 38, 540, 430, 115, 34, 0, 0, 0, false, 130),
            new("resume", "Resume", "Settings Menu", 57, 80, 560, 120, 36, 0, 0, 0, false, 190),
            new("map", "Map", null, int.MaxValue, 340, 560, 90, 36, 0, 0, -8, false),
            new("continue", "Continue", null, int.MaxValue, 520, 560, 140, 36, 0, 0, 8, false),
            new("options", "Options", null, int.MaxValue, 760, 560, 130, 36, 0, 0, 0, false),
        ];
        StableRandom random = new(seed);
        int[] missedFrames = new int[entities.Length];
        List<IReadOnlyList<TextRect>> observations = [];
        List<IReadOnlyList<TextRect>> expected = [];
        int mergeOffset = random.Next(7);

        for (int frame = 0; frame < frameCount; frame++)
        {
            TextRect[] truth = entities
                .Select(entity => CreateTruth(entity, frame, stabilizeChanges: false))
                .ToArray();
            expected.Add(entities
                .Select(entity => CreateTruth(entity, frame, stabilizeChanges: true))
                .ToArray());

            List<TextRect> current = [];
            bool mergeNewGame = IsMergeFrame(frame, mergeOffset);
            if (mergeNewGame)
            {
                TextRect first = truth[1];
                TextRect second = truth[2];
                current.Add(AddNoise(
                    first with
                    {
                        SourceText = "New Game",
                        Width = (second.X + second.Width) - first.X,
                        Context = "new+game",
                    },
                    random,
                    allowTextError: true));
                missedFrames[1] = 0;
                missedFrames[2] = 0;
            }

            for (int index = 0; index < entities.Length; index++)
            {
                if (mergeNewGame && index is 1 or 2)
                {
                    continue;
                }

                bool drop = missedFrames[index] < 2 && random.Chance(0.08);
                if (drop)
                {
                    missedFrames[index]++;
                    continue;
                }
                missedFrames[index] = 0;

                TextRect noisy = AddNoise(truth[index], random, allowTextError: true);
                bool fivePartBoundary = index == 0 && (frame + mergeOffset) % 29 == 11;
                bool split = fivePartBoundary || (noisy.SourceText.Length >= 4 && random.Chance(0.07));
                if (split)
                {
                    int partCount = fivePartBoundary ? 5 : 2 + random.Next(3);
                    current.AddRange(Split(noisy, partCount));
                }
                else
                {
                    current.Add(noisy);
                }
            }

            if ((frame + mergeOffset) % 17 == 6 || random.Chance(0.025))
            {
                current.Add(new(
                    $"Noise-{frame}",
                    1100 + random.Next(600),
                    100 + random.Next(700),
                    70 + random.Next(100),
                    24 + random.Next(18),
                    20,
                    false)
                {
                    Angle = random.Next(-20, 21),
                    Context = "false-positive",
                });
            }

            Shuffle(current, random);
            observations.Add(current);
        }

        return new(seed, imageSize, observations, expected);
    }

    public static double Measure(
        IReadOnlyList<IReadOnlyList<TextRect>> expectedFrames,
        IReadOnlyList<IReadOnlyList<TextRect>> actualFrames)
    {
        Assert.Equal(expectedFrames.Count, actualFrames.Count);
        return expectedFrames.Zip(actualFrames, ScoreFrame).Average();
    }

    private static TextRect CreateTruth(Entity entity, int frame, bool stabilizeChanges)
    {
        bool changed = frame >= entity.ChangeFrame;
        if (stabilizeChanges && frame == entity.ChangeFrame)
        {
            changed = false;
        }
        double angle = entity.Id == "quest" && frame >= 48
            ? 1
            : entity.Angle;
        if (stabilizeChanges && entity.Id == "quest" && frame < 51)
        {
            angle = entity.Angle;
        }
        return new(
            changed ? entity.ChangedText! : entity.InitialText,
            entity.X + (entity.VelocityX * frame),
            entity.Y + (entity.VelocityY * frame),
            changed ? entity.EffectiveChangedWidth : entity.Width,
            entity.Height,
            entity.Height * 0.8,
            entity.MultiLine)
        {
            Angle = angle,
            Context = entity.Id,
        };
    }

    private static TextRect AddNoise(TextRect source, StableRandom random, bool allowTextError)
    {
        bool outlier = random.Chance(0.018);
        double xNoise = outlier ? random.Next(-24, 25) : random.Next(-3, 4);
        double yNoise = outlier ? random.Next(-16, 17) : random.Next(-2, 3);
        string text = allowTextError && random.Chance(0.11)
            ? Mutate(source.SourceText, random)
            : source.SourceText;
        return source with
        {
            SourceText = text,
            X = source.X + xNoise,
            Y = source.Y + yNoise,
            Width = Math.Max(8, source.Width + random.Next(-5, 6)),
            Height = Math.Max(8, source.Height + random.Next(-2, 3)),
            FontSize = Math.Max(6, source.FontSize + (random.Next(-2, 3) * 0.5)),
            Angle = source.Angle + (random.Next(-3, 4) * 0.5),
            MultiLine = random.Chance(0.015) ? !source.MultiLine : source.MultiLine,
        };
    }

    private static IEnumerable<TextRect> Split(TextRect source, int requestedPartCount)
    {
        string[] parts = CreateParts(source.SourceText, requestedPartCount);
        double partWidth = source.Width / parts.Length;
        for (int index = 0; index < parts.Length; index++)
        {
            yield return source with
            {
                SourceText = parts[index],
                X = source.X + (partWidth * index),
                Width = partWidth,
                Context = $"{source.Context}:part-{index}",
            };
        }
    }

    private static string[] CreateParts(string text, int requestedPartCount)
    {
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2 && words.Length <= requestedPartCount)
        {
            return words;
        }

        string compact = string.Concat(words);
        int partCount = Math.Min(requestedPartCount, compact.Length);
        List<string> parts = [];
        int offset = 0;
        for (int part = 0; part < partCount; part++)
        {
            int remainingParts = partCount - part;
            int length = (compact.Length - offset + remainingParts - 1) / remainingParts;
            parts.Add(compact.Substring(offset, length));
            offset += length;
        }
        return parts.ToArray();
    }

    private static string Mutate(string text, StableRandom random)
    {
        if (text.Length == 0)
        {
            return text;
        }
        int index = random.Next(text.Length);
        char[] characters = text.ToCharArray();
        characters[index] = characters[index] == '8' ? 'B' : '8';
        return new(characters);
    }

    private static bool IsMergeFrame(int frame, int offset)
        => (frame + offset) % 31 is 8 or 9;

    private static void Shuffle(List<TextRect> values, StableRandom random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int other = random.Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }

    private static double ScoreFrame(
        IReadOnlyList<TextRect> expected,
        IReadOnlyList<TextRect> actual)
    {
        int count = Math.Max(expected.Count, actual.Count);
        if (count == 0)
        {
            return 1;
        }

        double[,] scores = new double[count + 1, count + 1];
        for (int expectedIndex = 0; expectedIndex < expected.Count; expectedIndex++)
        {
            for (int actualIndex = 0; actualIndex < actual.Count; actualIndex++)
            {
                scores[expectedIndex + 1, actualIndex + 1] = ScoreRect(
                    expected[expectedIndex],
                    actual[actualIndex]);
            }
        }

        int[] assignment = SolveMaximumWeightAssignment(scores, count);
        double score = 0;
        for (int expectedIndex = 0; expectedIndex < expected.Count; expectedIndex++)
        {
            int actualIndex = assignment[expectedIndex];
            if (actualIndex < actual.Count)
            {
                score += scores[expectedIndex + 1, actualIndex + 1];
            }
        }
        return score / count;
    }

    private static double ScoreRect(TextRect expected, TextRect actual)
    {
        double text = expected.SourceText == actual.SourceText ? 1 : 0;
        double geometry = IntersectionOverUnion(expected, actual);
        double fontSize = RatioSimilarity(expected.FontSize, actual.FontSize);
        double angle = Math.Max(0, 1 - (AngleDifference(expected.Angle, actual.Angle) / 10));
        double multiLine = expected.MultiLine == actual.MultiLine ? 1 : 0;
        return (text * 0.45)
            + (geometry * 0.35)
            + (fontSize * 0.08)
            + (angle * 0.07)
            + (multiLine * 0.05);
    }

    private static int[] SolveMaximumWeightAssignment(double[,] weights, int count)
    {
        const double maximumWeight = 1;
        double[] rowPotential = new double[count + 1];
        double[] columnPotential = new double[count + 1];
        int[] matching = new int[count + 1];
        int[] path = new int[count + 1];
        for (int row = 1; row <= count; row++)
        {
            matching[0] = row;
            int column = 0;
            double[] minimum = Enumerable.Repeat(double.PositiveInfinity, count + 1).ToArray();
            bool[] used = new bool[count + 1];
            do
            {
                used[column] = true;
                int currentRow = matching[column];
                double delta = double.PositiveInfinity;
                int nextColumn = 0;
                for (int candidateColumn = 1; candidateColumn <= count; candidateColumn++)
                {
                    if (used[candidateColumn])
                    {
                        continue;
                    }
                    double reducedCost = maximumWeight - weights[currentRow, candidateColumn]
                        - rowPotential[currentRow]
                        - columnPotential[candidateColumn];
                    if (reducedCost < minimum[candidateColumn])
                    {
                        minimum[candidateColumn] = reducedCost;
                        path[candidateColumn] = column;
                    }
                    if (minimum[candidateColumn] < delta)
                    {
                        delta = minimum[candidateColumn];
                        nextColumn = candidateColumn;
                    }
                }
                for (int candidateColumn = 0; candidateColumn <= count; candidateColumn++)
                {
                    if (used[candidateColumn])
                    {
                        rowPotential[matching[candidateColumn]] += delta;
                        columnPotential[candidateColumn] -= delta;
                    }
                    else
                    {
                        minimum[candidateColumn] -= delta;
                    }
                }
                column = nextColumn;
            }
            while (matching[column] != 0);

            do
            {
                int previousColumn = path[column];
                matching[column] = matching[previousColumn];
                column = previousColumn;
            }
            while (column != 0);
        }

        int[] result = new int[count];
        for (int column = 1; column <= count; column++)
        {
            result[matching[column] - 1] = column - 1;
        }
        return result;
    }

    private static double IntersectionOverUnion(TextRect first, TextRect second)
    {
        double left = Math.Max(first.X, second.X);
        double top = Math.Max(first.Y, second.Y);
        double right = Math.Min(first.X + first.Width, second.X + second.Width);
        double bottom = Math.Min(first.Y + first.Height, second.Y + second.Height);
        double intersection = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        double union = (first.Width * first.Height) + (second.Width * second.Height) - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    private static double RatioSimilarity(double first, double second)
    {
        double maximum = Math.Max(Math.Abs(first), Math.Abs(second));
        return maximum <= double.Epsilon ? 1 : Math.Min(Math.Abs(first), Math.Abs(second)) / maximum;
    }

    private static double AngleDifference(double first, double second)
    {
        double difference = Math.Abs(first - second) % 360;
        return Math.Min(difference, 360 - difference);
    }

    private sealed record Entity(
        string Id,
        string InitialText,
        string? ChangedText,
        int ChangeFrame,
        double X,
        double Y,
        double Width,
        double Height,
        double VelocityX,
        double VelocityY,
        double Angle,
        bool MultiLine,
        double ChangedWidth = 0)
    {
        public double EffectiveChangedWidth { get; } = ChangedWidth > 0 ? ChangedWidth : Width;
    }

    private sealed class StableRandom(int seed)
    {
        private uint state = seed == 0 ? 0x9E3779B9 : unchecked((uint)seed);

        public int Next(int maximum)
            => maximum <= 0 ? throw new ArgumentOutOfRangeException(nameof(maximum)) : (int)(this.NextUInt32() % maximum);

        public int Next(int minimum, int maximum)
            => minimum + this.Next(maximum - minimum);

        public bool Chance(double probability)
            => this.NextUInt32() / ((double)uint.MaxValue + 1) < probability;

        private uint NextUInt32()
        {
            uint value = this.state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            this.state = value;
            return value;
        }
    }
}

internal sealed record RandomizedScenario(
    int Seed,
    Size ImageSize,
    IReadOnlyList<IReadOnlyList<TextRect>> Observations,
    IReadOnlyList<IReadOnlyList<TextRect>> Expected);
