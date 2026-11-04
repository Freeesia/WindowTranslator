namespace WindowTranslator;

public static partial class OcrUtility
{
    private static double Median(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        return (values[(values.Length - 1) / 2] + values[values.Length / 2]) / 2;
    }

    private static List<TextRect> RefineParagraphs(
        List<TextRect> source,
        List<List<TextRect>> paragraphs,
        Dictionary<TextRect, double> textHeights,
        Modules.BasicOcrParam options,
        string language,
        string separator,
        double xThreshold)
    {
        var count = source.Count;
        if (count == 0)
            return [];
        var index = new Dictionary<TextRect, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < count; i++)
            index.Add(source[i], i);
        var originalGroups = paragraphs.Select(p => p.Select(r => index[r]).ToArray()).ToArray();
        var originalGroupOf = new int[count];
        for (var i = 0; i < originalGroups.Length; i++)
            foreach (var id in originalGroups[i])
                originalGroupOf[id] = i;

        var projections = new Dictionary<double, RectInfo[]>();
        RectInfo[] Project(double angle)
        {
            if (!projections.TryGetValue(angle, out var bounds))
            {
                var radians = angle * Math.PI / 180;
                bounds = source.Select(r => ProjectTextBounds(r, Math.Cos(radians), Math.Sin(radians))).ToArray();
                projections.Add(angle, bounds);
            }
            return bounds;
        }
        bool Overlap(int i, int j, double angle)
        {
            var a = Project(angle)[i];
            var b = Project(angle)[j];
            return Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X) > .5 * Math.Min(a.Width, b.Width);
        }
        double Difference(int i, int j) => Math.IEEERemainder(source[j].Angle - source[i].Angle, 360);
        // 短い行の角度揺れを、同じ列に重なる行群の中央値で補正する。
        var axes = Enumerable.Range(0, count).Select(i => source[i].Angle + Median(
            Enumerable.Range(0, count).Where(j => Math.Abs(Difference(i, j)) <= 2 * MergeAngleThreshold
                && Overlap(i, j, source[i].Angle)).Select(j => Difference(i, j)))).ToArray();
        bool SameDirection(int i, int j)
            => Math.Abs(Math.IEEERemainder(source[i].Angle - axes[i], 360)) <= MergeAngleThreshold
                && Math.Abs(Math.IEEERemainder(source[j].Angle - axes[i], 360)) <= MergeAngleThreshold;
        double Top(int i, double angle)
        {
            var radians = angle * Math.PI / 180;
            return -source[i].X * Math.Sin(radians) + source[i].Y * Math.Cos(radians);
        }
        bool SimilarHeight(int i, int j)
            => Math.Max(source[i].Height, source[j].Height) <= 1.3 * Math.Min(source[i].Height, source[j].Height);
        (int[] Next, double[] Steps) Neighbors(bool similarHeight)
        {
            var next = Enumerable.Repeat(-1, count).ToArray();
            var steps = new double[count];
            for (var i = 0; i < count; i++)
            {
                var best = double.PositiveInfinity;
                for (var j = 0; j < count; j++)
                {
                    if (i == j || !SameDirection(i, j) || !Overlap(i, j, axes[i]) || similarHeight && !SimilarHeight(i, j))
                        continue;
                    var step = similarHeight
                        ? Project(axes[i])[j].Y - Project(axes[i])[i].Y
                        : Top(j, axes[i]) - Top(i, axes[i]);
                    if (step <= .65 * Math.Min(source[i].Height, source[j].Height) || step >= best)
                        continue;
                    best = steps[i] = step;
                    next[i] = j;
                }
            }
            return (next, steps);
        }
        double? Pitch(int i, (int[] Next, double[] Steps) neighbors, bool similarHeight)
        {
            var samples = Enumerable.Range(0, count).Where(j => neighbors.Next[j] >= 0
                    && SameDirection(i, j) && Overlap(i, j, axes[i]) && (!similarHeight || SimilarHeight(i, j))
                    && neighbors.Steps[j] <= 4 * (source[j].Height + source[neighbors.Next[j]].Height) / 2)
                .OrderBy(j => Math.Abs((similarHeight ? Project(axes[i])[j].Y : Top(j, axes[i]))
                    - (similarHeight ? Project(axes[i])[i].Y : Top(i, axes[i]))))
                .Take(6).Select(j => neighbors.Steps[j]).Order().ToArray();
            return samples.Length >= 2 ? Median(samples.Take((samples.Length + 1) / 2)) : null;
        }

        // 他の列や見出しをまたいで推定した行送りで、離れた段落を結合しない。
        var similarNeighbors = Neighbors(true);
        var pitches = Enumerable.Range(0, count).Select(i => Pitch(i, similarNeighbors, true)).ToArray();
        var groups = new List<List<int>>();
        foreach (var ids in originalGroups)
        {
            var group = new List<int>();
            foreach (var id in ids)
            {
                if (group.Count > 0)
                {
                    var last = group[^1];
                    if (pitches[last] is double a && pitches[id] is double b
                        && Project(axes[last])[id].Y - Project(axes[last])[last].Y > Math.Max(a, b) * options.LineSpacingTolerance)
                    {
                        groups.Add(group);
                        group = [];
                    }
                }
                group.Add(id);
            }
            groups.Add(group);
        }

        bool Short(int i) => LanguageUtility.IsSpaceLang(language)
            ? LanguageUtility.WordCount(source[i].SourceText) <= 2 : source[i].SourceText.Length <= 8;
        var rowAxes = Enumerable.Range(0, count).Select(i => source[i].Angle + Median(
            Enumerable.Range(0, count).Where(j => Math.Abs(Difference(i, j)) <= 2 * MergeAngleThreshold)
                .Select(j => Difference(i, j)))).ToArray();
        bool SameRow(int i, int j)
        {
            if (originalGroupOf[i] == originalGroupOf[j] || Math.Abs(Difference(i, j)) > MergeAngleThreshold)
                return false;
            var a = Project(rowAxes[i])[i];
            var b = Project(rowAxes[i])[j];
            return Math.Abs(a.Y - b.Y) <= Math.Min(source[i].Height, source[j].Height) / 4
                && Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X) < .25 * Math.Min(a.Width, b.Width);
        }
        var peers = Enumerable.Range(0, count).Select(i => Enumerable.Range(0, count).Any(j => SameRow(i, j))).ToArray();
        var fields = source.Select(r => NumericField().IsMatch(r.SourceText)).ToArray();
        bool Similar(double a, double b) => Math.Abs(a - b) <= options.FontSizeThrethold * Math.Min(a, b);
        var neighbors = Neighbors(false);
        var localPitches = Enumerable.Range(0, count).Select(i => Pitch(i, neighbors, false)).ToArray();
        bool Close(int i, int next)
        {
            var pitch = localPitches[i] ?? 1.5 * (source[i].Height + source[next].Height) / 2;
            return neighbors.Steps[i] <= pitch * options.LineSpacingTolerance;
        }
        double DecisionFont(int i)
        {
            // 独立した記号を含む一行だけが高いときは、同じ列の前後の本文行で文字サイズを補う。
            // 出力には認識時の文字サイズを残す。
            var next = neighbors.Next[i];
            if (next < 0 || !StandaloneSymbol().IsMatch(source[i].SourceText) || Short(i) || Short(next) || !Close(i, next))
                return source[i].FontSize;
            var previous = Enumerable.Range(0, count).Where(j => neighbors.Next[j] == i && !Short(j) && Close(j, i))
                .OrderBy(j => neighbors.Steps[j]).FirstOrDefault(-1);
            return previous >= 0 && source[previous].FontSize < source[i].FontSize && source[next].FontSize < source[i].FontSize
                && Similar(source[previous].FontSize, source[next].FontSize)
                ? (source[previous].FontSize + source[next].FontSize) / 2 : source[i].FontSize;
        }
        bool CanJoin(int previous, int current)
        {
            if (originalGroupOf[previous] != originalGroupOf[current]
                && (Short(previous) && Short(current) && peers[previous] && peers[current] || fields[previous] || fields[current]))
                return false;
            var fontMatch = Similar(DecisionFont(previous), DecisionFont(current));
            // 一語の末尾行も使う。アイコンや大文字で行の高さが変わった場合の補助情報。
            var wordMatch = textHeights.TryGetValue(source[previous], out var firstHeight)
                && textHeights.TryGetValue(source[current], out var secondHeight) && Similar(firstHeight, secondHeight);
            if (!fontMatch && !wordMatch)
                return false;
            var angle = source[previous].Angle + Difference(previous, current) / 2;
            var a = Project(angle)[previous];
            var b = Project(angle)[current];
            if (b.Y - a.Y <= .65 * Math.Min(source[previous].Height, source[current].Height))
                return false;
            if (Short(previous) && Short(current))
            {
                // 短い項目の繰り返しだけでは本文の行送りと区別できないため、既存の行間設定を使う。
                var leading = (DecisionFont(previous) + DecisionFont(current)) / 2 * options.LeadingThrethold;
                var aligned = Math.Abs(a.X - b.X) < xThreshold
                    || Math.Abs(a.X + a.Width / 2 - b.X - b.Width / 2) < xThreshold;
                if (options.IsAvoidMergeList || !aligned || Math.Abs(b.Y - a.Bottom) >= leading)
                    return false;
            }
            return true;
        }

        // 一つの見出しの幅で列全体を固定せず、各行から直下に重なる行を探す。
        var parent = Enumerable.Range(0, count).ToArray();
        static int Root(int[] parents, int id)
        {
            while (parents[id] != id)
            {
                parents[id] = parents[parents[id]];
                id = parents[id];
            }
            return id;
        }
        for (var i = 0; i < count; i++)
        {
            var next = neighbors.Next[i];
            if (next < 0)
                continue;
            if (Close(i, next))
                parent[Root(parent, i)] = Root(parent, next);
        }
        var proposals = new List<List<int>>();
        foreach (var component in Enumerable.Range(0, count).GroupBy(i => Root(parent, i)))
        {
            var firstAngle = source[component.First()].Angle;
            var angle = firstAngle + Median(component.Select(i => Math.IEEERemainder(source[i].Angle - firstAngle, 360)));
            var height = Median(component.Select(i => source[i].Height));
            var proposal = new List<int>();
            foreach (var id in component.OrderBy(i => Top(i, angle)).ThenBy(i => Project(angle)[i].X))
            {
                if (proposal.Count > 0)
                {
                    var b = Project(angle)[id];
                    var left = proposal.Min(i => Project(angle)[i].X);
                    var right = proposal.Max(i => Project(angle)[i].Right);
                    var wide = b.Width >= 2 * (right - left) && Math.Abs((b.X + b.Right - left - right) / 2) > height;
                    var indent = b.X - Project(angle)[proposal[^1]].X;
                    if (wide || options.IsAvoidMergeList && Short(proposal[^1]) && Short(id) || proposal.Count > 1 && indent > height)
                    {
                        proposals.Add(proposal);
                        proposal = [];
                    }
                }
                proposal.Add(id);
            }
            proposals.Add(proposal);
        }

        var groupOf = new int[count];
        for (var i = 0; i < groups.Count; i++)
            foreach (var id in groups[i])
                groupOf[id] = i;
        var joined = Enumerable.Range(0, groups.Count).ToArray();
        foreach (var proposal in proposals)
        {
            var positions = proposal.Select((id, position) => (id, position)).ToDictionary(p => p.id, p => p.position);
            for (var i = 1; i < proposal.Count; i++)
            {
                var previous = proposal[i - 1];
                var current = proposal[i];
                var first = Root(joined, groupOf[previous]);
                var second = Root(joined, groupOf[current]);
                if (first == second || groups[first][^1] != previous || groups[second][0] != current)
                    continue;
                // 段落の末尾から次の先頭へ、同じ読み順の連続した行だけを追加する。
                var members = groups[first].Concat(groups[second]).ToArray();
                if (members.Any(id => !positions.ContainsKey(id))
                    || members.Zip(members.Skip(1)).Any(pair => positions[pair.First] + 1 != positions[pair.Second])
                    || !CanJoin(previous, current))
                    continue;
                joined[second] = first;
                groups[first].AddRange(groups[second]);
            }
        }
        var results = new List<TextRect>();
        for (var i = 0; i < groups.Count; i++)
        {
            if (Root(joined, i) != i)
                continue;
            var members = groups[i].Select(id => source[id]).ToList();
            var firstAngle = members[0].Angle;
            var angle = firstAngle + Median(members.Select(r => Math.IEEERemainder(r.Angle - firstAngle, 360)));
            results.Add(MergeTextMembers(members, angle, separator, members.Count > 1));
        }
        return results;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[^:：]+[:：]\s*[+\-\p{Nd}][\p{Nd}.,()%/+\-\s]*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex NumericField();

    [System.Text.RegularExpressions.GeneratedRegex(@"(?:^|\s)[\p{S}\p{P}]+(?:\s|$)", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex StandaloneSymbol();
}
