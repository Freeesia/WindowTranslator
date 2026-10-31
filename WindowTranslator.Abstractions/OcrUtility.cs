using System.Text.RegularExpressions;

namespace WindowTranslator;

/// <summary>
/// OCRに関連するユーティリティメソッドを提供します。
/// </summary>
public static partial class OcrUtility
{
    private const double MergeAngleThreshold = 3;

    /// <summary>
    /// 同じOCR対象範囲の矩形を、傾きを考慮して行・段落へ結合する。
    /// </summary>
    /// <param name="source">結合前の単語または行の矩形</param>
    /// <param name="imageSize">矩形と同じ倍率の全体画像サイズ</param>
    /// <param name="options">共通の認識設定</param>
    /// <param name="language">原文言語</param>
    /// <returns>読み順に連結した段落の矩形</returns>
    public static IReadOnlyList<TextRect> MergeTextRects(
        IEnumerable<TextRect> source,
        System.Drawing.Size imageSize,
        Modules.BasicOcrParam options,
        string language)
    {
        var directions = new List<List<TextRect>>();
        foreach (var rect in source.Where(r => !string.IsNullOrWhiteSpace(r.SourceText) && r.Width > 0 && r.Height > 0))
        {
            var direction = directions.Find(group => Math.Abs(Math.IEEERemainder(group[0].Angle - rect.Angle, 360)) < MergeAngleThreshold);
            if (direction is null)
                directions.Add([rect]);
            else
                direction.Add(rect);
        }

        var xThreshold = options.XPosThrethold * imageSize.Width;
        var yThreshold = options.YPosThrethold * imageSize.Height;
        var separator = LanguageUtility.IsSpaceLang(language) ? " " : string.Empty;
        var results = new List<TextRect>();
        foreach (var direction in directions)
        {
            // 短い行だけ角度が揺れることがあるため、列の代表角度には中央値を使う。
            var firstAngle = direction[0].Angle;
            var angle = firstAngle + direction.Select(r => Math.IEEERemainder(r.Angle - firstAngle, 360)).Order().ElementAt(direction.Count / 2);
            var radians = angle * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            double Top(TextRect rect) => -rect.X * sin + rect.Y * cos;
            var projectedBounds = new Dictionary<TextRect, RectInfo>(ReferenceEqualityComparer.Instance);
            RectInfo Bounds(TextRect rect)
            {
                if (!projectedBounds.TryGetValue(rect, out var bounds))
                {
                    bounds = ProjectTextBounds(rect, cos, sin);
                    projectedBounds.Add(rect, bounds);
                }
                return bounds;
            }
            bool SameColumn(RectInfo first, RectInfo second)
                => first.X < second.Right && second.X < first.Right;
            bool SimilarSize(TextRect first, TextRect second)
                => Math.Abs(first.FontSize - second.FontSize) / Math.Min(first.FontSize, second.FontSize) <= options.FontSizeThrethold;
            bool AvoidList(TextRect first, TextRect second)
                => options.IsAvoidMergeList && (LanguageUtility.IsSpaceLang(language)
                    ? LanguageUtility.WordCount(first.SourceText) <= 2 && LanguageUtility.WordCount(second.SourceText) <= 2
                    : first.SourceText.Length <= 8 && second.SourceText.Length <= 8);

            // 単語の間隔を行送りの推定へ混ぜないよう、先に同じ行の断片を結合する。
            var remaining = direction.OrderBy(Top).ThenBy(r => Bounds(r).X).ToList();
            var lines = new List<TextRect>();
            while (remaining.Count > 0)
            {
                var members = new List<TextRect> { remaining[0] };
                remaining.RemoveAt(0);
                var line = members[0];
                var changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (var rect in remaining.ToArray())
                    {
                        var bounds = Bounds(line);
                        var other = Bounds(rect);
                        var gap = Math.Min(Math.Abs(other.X - bounds.Right), Math.Abs(bounds.X - other.Right));
                        if (Math.Abs(Top(line) - Top(rect)) >= yThreshold
                            || !(bounds.OverlapsWith(other) || SimilarSize(line, rect)
                                && gap < (line.FontSize + rect.FontSize) / 2 * options.SpacingThreshold))
                            continue;
                        members.Add(rect);
                        remaining.Remove(rect);
                        members.Sort((first, second) => Bounds(first).X.CompareTo(Bounds(second).X));
                        line = MergeTextMembers(members, angle, separator, false);
                        changed = true;
                    }
                }
                lines.Add(line);
            }

            // 列ごとに行を並べ、隣接行の距離だけから通常の行送りを推定する。
            var columns = new List<List<TextRect>>();
            foreach (var line in lines.OrderBy(Top).ThenBy(r => Bounds(r).X))
            {
                // 字下げされた折り返し行も行送りの推定に含める。段落の結合には位置設定を使う。
                var column = columns.Find(group => SameColumn(Bounds(group[0]), Bounds(line)));
                if (column is null)
                    columns.Add([line]);
                else
                    column.Add(line);
            }
            foreach (var column in columns)
            {
                var lineHeight = column.Select(r => r.Height).Order().ElementAt(column.Count / 2);
                var steps = new List<double>();
                for (var i = 1; i < column.Count; i++)
                {
                    var previous = column[i - 1];
                    var current = column[i];
                    if (!SimilarSize(previous, current) || AvoidList(previous, current))
                        continue;
                    // アイコンを含む一行だけが高くなっても、段落間の空きを過小評価しない。
                    var step = (Top(current) - Top(previous)) / lineHeight;
                    if (step > .65 && step <= 4)
                        steps.Add(step);
                }
                steps.Sort();
                var count = (steps.Count + 1) / 2;
                // 距離が1つだけでは段落間隔と区別できないので、既存の行間設定を使う。
                var normalStep = steps.Count >= 2 ? (steps[(count - 1) / 2] + steps[count / 2]) / 2 : double.NaN;
                var paragraph = new List<TextRect>();
                foreach (var line in column)
                {
                    if (paragraph.Count > 0)
                    {
                        var last = paragraph[^1];
                        var current = Bounds(line);
                        var left = paragraph.Min(r => Bounds(r).X);
                        var right = paragraph.Max(r => Bounds(r).Right);
                        var aligned = Math.Abs(left - current.X) < xThreshold && current.Width < (right - left) * 2
                            || Math.Abs((left + right) / 2 - current.X - current.Width / 2) < xThreshold;
                        var step = Top(line) - Top(last);
                        var close = double.IsFinite(normalStep)
                            ? step > lineHeight * .65 && step <= lineHeight * normalStep * options.LineSpacingTolerance
                            : Math.Abs(step - last.Height) < (last.FontSize + line.FontSize) / 2 * options.LeadingThrethold;
                        var fontSize = paragraph.Average(r => r.FontSize);
                        var similarSize = Math.Abs(fontSize - line.FontSize) / Math.Min(fontSize, line.FontSize) <= options.FontSizeThrethold;
                        if (!aligned || !similarSize || AvoidList(last, line) || !close)
                        {
                            results.Add(MergeTextMembers(paragraph, angle, separator, paragraph.Count > 1));
                            paragraph.Clear();
                        }
                    }
                    paragraph.Add(line);
                }
                results.Add(MergeTextMembers(paragraph, angle, separator, paragraph.Count > 1));
            }
        }
        return results;
    }

    private static RectInfo ProjectTextBounds(TextRect rect, double cos, double sin)
    {
        var radians = rect.Angle * Math.PI / 180;
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        var left = double.PositiveInfinity;
        var top = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var bottom = double.NegativeInfinity;
        ReadOnlySpan<(double X, double Y)> corners = [(0, 0), (rect.Width, 0), (0, rect.Height), (rect.Width, rect.Height)];
        foreach (var (dx, dy) in corners)
        {
            var x = rect.X + dx * c - dy * s;
            var y = rect.Y + dx * s + dy * c;
            var projectedX = x * cos + y * sin;
            var projectedY = -x * sin + y * cos;
            left = Math.Min(left, projectedX);
            top = Math.Min(top, projectedY);
            right = Math.Max(right, projectedX);
            bottom = Math.Max(bottom, projectedY);
        }
        return new(left, top, right - left, bottom - top);
    }

    private static TextRect MergeTextMembers(List<TextRect> members, double angle, string separator, bool multiLine)
    {
        if (members.Count == 1)
            return members[0];
        var radians = angle * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var bounds = members.Select(r => ProjectTextBounds(r, cos, sin)).ToArray();
        var left = bounds.Min(r => r.X);
        var top = bounds.Min(r => r.Y);
        return members[0] with
        {
            SourceText = string.Join(separator, members.Select(r => r.SourceText.Trim())),
            X = left * cos - top * sin,
            Y = left * sin + top * cos,
            Width = bounds.Max(r => r.Right) - left,
            Height = bounds.Max(r => r.Bottom) - top,
            FontSize = members.Average(r => r.FontSize),
            Angle = angle,
            MultiLine = multiLine || members.Any(r => r.MultiLine),
        };
    }

#if WINDOWS
    private const double OverlapThreshold = 0.5;

    /// <summary>
    /// 1回のキャプチャーに含まれるOCR対象範囲を順番に切り出して認識する
    /// </summary>
    /// <param name="input">全体画像とOCR対象範囲</param>
    /// <param name="recognizeAsync">切り出した画像と拡大後の全体画像サイズを認識する処理</param>
    /// <param name="scale">OCR前に画像へ適用する拡大率</param>
    /// <param name="brightness">OCR前に適用する明るさ</param>
    /// <param name="contrast">OCR前に適用するコントラスト</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>全体画像の座標系へ変換し、優先度の重複を除外した認識結果</returns>
    public static async ValueTask<IReadOnlyList<TextRect>> RecognizeRegionsAsync(
        Modules.OcrCaptureInput input,
        Func<Windows.Graphics.Imaging.SoftwareBitmap, System.Drawing.Size, ValueTask<IReadOnlyList<TextRect>>> recognizeAsync,
        double scale = 1,
        int brightness = 0,
        int contrast = 0,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TextRect>();
        var scaledSourceSize = new System.Drawing.Size(
            (int)(input.Source.PixelWidth * scale),
            (int)(input.Source.PixelHeight * scale));

        foreach (var region in input.Regions)
        {
            var bounds = region.Bounds;
            var isSource = bounds.X == 0
                && bounds.Y == 0
                && bounds.Width == input.Source.PixelWidth
                && bounds.Height == input.Source.PixelHeight;
            var bitmap = isSource
                ? input.Source
                : input.Source.Crop((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height);
            var workingBitmap = bitmap;

            try
            {
                workingBitmap = await bitmap.ResizeSoftwareBitmapAsync(scale, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (brightness != 0 || contrast != 0)
                {
                    if (workingBitmap == bitmap)
                    {
                        workingBitmap = Windows.Graphics.Imaging.SoftwareBitmap.Copy(bitmap);
                    }
                    workingBitmap.AdjustBrightnessContrastInPlace(brightness, contrast);
                }

                var regionResults = await recognizeAsync(workingBitmap, scaledSourceSize).ConfigureAwait(false);
                var offsetResults = regionResults
                    .Select(r => r with
                    {
                        X = r.X / scale,
                        Y = r.Y / scale,
                        Width = r.Width / scale,
                        Height = r.Height / scale,
                        FontSize = r.FontSize / scale,
                    })
                    .Select(r => r.Offset(bounds.X, bounds.Y, region.Keyword))
                    .Where(r => !IsCoveredBy(r, results))
                    .ToArray();
                results.AddRange(offsetResults);
            }
            finally
            {
                if (workingBitmap != bitmap)
                {
                    workingBitmap.Dispose();
                }
                if (!isSource)
                {
                    bitmap.Dispose();
                }
            }
        }

        return results;
    }

    private static bool IsCoveredBy(TextRect text, List<TextRect> recognized)
    {
        var box = text.GetRotatedBoundingBox();
        return recognized.Any(r => IntersectionRatio(r.GetRotatedBoundingBox(), box) >= OverlapThreshold);
    }

    private static double IntersectionRatio(RectInfo area, RectInfo other)
    {
        var width = Math.Max(0, Math.Min(area.Right, other.Right) - Math.Max(area.Left, other.Left));
        var height = Math.Max(0, Math.Min(area.Bottom, other.Bottom) - Math.Max(area.Top, other.Top));
        return width * height / (other.Width * other.Height);
    }
#endif

    [GeneratedRegex(@"^[\s\p{S}\p{P}\d]+$")]
    public static partial Regex AllSymbolOrSpace();
}
