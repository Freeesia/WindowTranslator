using WindowTranslator.Plugin.OneOcrPlugin;

namespace WindowTranslator.Plugin.BluePrinceOcrPlugin;

internal static class LineGeometry
{
    // 小さな傾きも保持する。外接矩形で高さが増えると段落判定が不安定になる。
    public static (double X, double Y, double Width, double Height, double Angle) Calculate(BoundingBox box)
    {
        var angle = Math.Atan2(box.y2 - box.y1, box.x2 - box.x1);
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var points = new[] { (box.x1, box.y1), (box.x2, box.y2), (box.x3, box.y3), (box.x4, box.y4) }
            .Select(p => (X: p.Item1 * cos + p.Item2 * sin, Y: -p.Item1 * sin + p.Item2 * cos)).ToArray();
        var x = points.Min(p => p.X);
        var y = points.Min(p => p.Y);
        return (x * cos - y * sin, x * sin + y * cos,
            points.Max(p => p.X) - x, points.Max(p => p.Y) - y, angle * 180 / Math.PI);
    }
}
