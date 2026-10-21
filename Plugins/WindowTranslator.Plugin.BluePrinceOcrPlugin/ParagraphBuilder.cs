namespace WindowTranslator.Plugin.BluePrinceOcrPlugin;

/// <summary>行の傾きを補正した座標で、空行と段組みを判定する。</summary>
internal static class ParagraphBuilder
{
    public static IReadOnlyList<TextRect> Build(IEnumerable<TextRect> source, double lineSpacingTolerance = BluePrinceOcrParam.DefaultLineSpacingTolerance, double fontSizeTolerance = .30)
    {
        var remaining = source.Where(r => !string.IsNullOrWhiteSpace(r.SourceText)
            && r.Width > 0 && r.Height > 0).OrderBy(r => r.Y).ToList();
        var allLines = remaining.ToArray();
        fontSizeTolerance = BluePrinceOcrParam.NormalizeFontSize(fontSizeTolerance);
        lineSpacingTolerance = BluePrinceOcrParam.Normalize(lineSpacingTolerance);
        var results = new List<TextRect>();
        while (remaining.Count > 0)
        {
            var first = remaining[0];
            remaining.RemoveAt(0);
            var angle = first.Angle * Math.PI / 180;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            (double X, double Y) Project(TextRect r)
                => (r.X * cos + r.Y * sin, -r.X * sin + r.Y * cos);
            var normalStep = EstimateLineSpacing(allLines, first, fontSizeTolerance);
            var lines = new List<TextRect> { first };
            var last = first;
            while (true)
            {
                var previous = Project(last);
                // 文字高を基準にすることで、解像度と認識スケールに追従する。
                var next = remaining.Where(r =>
                {
                    if (Math.Abs(r.Angle - first.Angle) > 3 ||
                        Math.Abs(TextHeight(r) - TextHeight(last)) / Math.Min(TextHeight(r), TextHeight(last)) > fontSizeTolerance)
                        return false;
                    var p = Project(r);
                    var height = (TextHeight(r) + TextHeight(last)) / 2;
                    var step = p.Y - previous.Y;
                    return step > height * .65 && step <= height * normalStep * lineSpacingTolerance
                        && Math.Abs(p.X - Project(first).X) <= height * 1.5;
                }).OrderBy(r => Project(r).Y).FirstOrDefault();
                if (next is null)
                    break;
                lines.Add(next);
                remaining.Remove(next);
                last = next;
            }

            // 全頂点を同一の座標系に投影し、回転を保持した段落矩形を作る。
            var points = lines.SelectMany(r =>
            {
                var a = r.Angle * Math.PI / 180;
                var c = Math.Cos(a);
                var s = Math.Sin(a);
                return new[] { (0d, 0d), (r.Width, 0d), (0d, r.Height), (r.Width, r.Height) }
                    .Select(p =>
                    {
                        var x = r.X + p.Item1 * c - p.Item2 * s;
                        var y = r.Y + p.Item1 * s + p.Item2 * c;
                        return (X: x * cos + y * sin, Y: -x * sin + y * cos);
                    });
            }).ToArray();
            var left = points.Min(p => p.X);
            var top = points.Min(p => p.Y);
            results.Add(first with
            {
                SourceText = string.Join(" ", lines.Select(r => r.SourceText.Trim())),
                X = left * cos - top * sin,
                Y = left * sin + top * cos,
                Width = points.Max(p => p.X) - left,
                Height = points.Max(p => p.Y) - top,
                FontSize = lines.Average(r => r.FontSize),
                MultiLine = lines.Count > 1,
            });
        }
        return results;
    }
    // 同じ列・向き・文字サイズの行から、直下の行までの距離を収集する。
    // 文字高で正規化し、短い側の半分の中央値を使うことで段落間の空白の影響を抑える。
    private static double EstimateLineSpacing(IReadOnlyList<TextRect> lines, TextRect first, double fontSizeTolerance)
    {
        var angle = first.Angle * Math.PI / 180;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        (double X, double Y) Project(TextRect r)
            => (r.X * cos + r.Y * sin, -r.X * sin + r.Y * cos);
        var origin = Project(first);
        var column = lines.Where(r => Math.Abs(r.Angle - first.Angle) <= 3
            && Math.Abs(TextHeight(r) - TextHeight(first)) / Math.Min(TextHeight(r), TextHeight(first)) <= fontSizeTolerance
            && Math.Abs(Project(r).X - origin.X) <= (TextHeight(r) + TextHeight(first)) * .75).ToArray();
        var steps = new List<double>();
        foreach (var line in column)
        {
            var start = Project(line);
            var nearest = column.Where(r => !ReferenceEquals(r, line))
                .Select(r => (Project(r).Y - start.Y) / ((TextHeight(r) + TextHeight(line)) / 2))
                .Where(step => step > .65 && step <= 4)
                .DefaultIfEmpty(double.NaN).Min();
            if (double.IsFinite(nearest))
                steps.Add(nearest);
        }
        if (steps.Count == 0)
            return 1.5;
        steps.Sort();
        var count = (steps.Count + 1) / 2;
        return (steps[(count - 1) / 2] + steps[count / 2]) / 2;
    }
    private static double TextHeight(TextRect line)
        => double.IsFinite(line.FontSize) && line.FontSize > 0 ? line.FontSize / .9 : line.Height;
}


