using System.Diagnostics;

namespace WindowTranslator.Plugin.OneOcrPlugin;

static class Utility
{
    private const string OneOcrDll = "oneocr.dll";
    public const string OneOcrModel = "oneocr.onemodel";
    private const string OnnxRuntimeDll = "onnxruntime.dll";
    public static string OneOcrPath { get; } = Path.Combine(PathUtility.UserDir, "OneOcr");

    private static async ValueTask<string?> GetInstallLocation(string appName)
    {
        var info = new ProcessStartInfo("powershell.exe", $"-Command \"(Get-AppxPackage -Name {appName}).InstallLocation\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        using var p = Process.Start(info);
        await p!.WaitForExitAsync().ConfigureAwait(false);
        if (p.ExitCode != 0)
        {
            return null;
        }
        var path = await p.StandardOutput.ReadToEndAsync();
        return path.Trim();
    }

    public static async ValueTask<string?> FindOneOcrPath()
    {
        var scketch = await GetInstallLocation("Microsoft.ScreenSketch").ConfigureAwait(false);
        if (!string.IsNullOrEmpty(scketch))
        {
            return Path.Combine(scketch, "SnippingTool");
        }
        return await GetInstallLocation("Microsoft.Photos").ConfigureAwait(false);
    }

    public static bool NeedCopyDll()
    {
        if (!File.Exists(Path.Combine(OneOcrPath, OneOcrDll)))
        {
            return true;
        }
        if (!File.Exists(Path.Combine(OneOcrPath, OneOcrModel)))
        {
            return true;
        }
        if (!File.Exists(Path.Combine(OneOcrPath, OnnxRuntimeDll)))
        {
            return true;
        }
        return false;
    }

    public static void CopyDll(string path)
    {
        Directory.CreateDirectory(OneOcrPath);
        File.Copy(Path.Combine(path, OneOcrDll), Path.Combine(OneOcrPath, OneOcrDll), true);
        File.Copy(Path.Combine(path, OneOcrModel), Path.Combine(OneOcrPath, OneOcrModel), true);
        File.Copy(Path.Combine(path, OnnxRuntimeDll), Path.Combine(OneOcrPath, OnnxRuntimeDll), true);
    }

    /// <summary>
    /// 傾いた矩形（4つの頂点）から適切な位置、サイズ、角度を計算する
    /// </summary>
    /// <param name="boundingBox">4つの頂点を持つ境界ボックス</param>
    /// <returns>左上角の座標、幅、高さ、角度（度）</returns>
    public static (double x, double y, double width, double height, double angle) CalculateOrientedRect(BoundingBox boundingBox)
    {
        // 4つの頂点を配列に格納
        var points = new (double x, double y)[]
        {
            (boundingBox.x1, boundingBox.y1),
            (boundingBox.x2, boundingBox.y2),
            (boundingBox.x3, boundingBox.y3),
            (boundingBox.x4, boundingBox.y4)
        };

        // 凸包を構築（すでに4点なので時計回りにソート）
        var sortedPoints = SortPointsClockwise(points);

        // 最小面積外接矩形を計算
        return CalculateMinimumAreaBoundingRectangle(sortedPoints);
    }

    /// <summary>
    /// 点を時計回りにソートする
    /// </summary>
    private static (double x, double y)[] SortPointsClockwise((double x, double y)[] points)
    {
        // 重心を計算
        var centroidX = points.Average(p => p.x);
        var centroidY = points.Average(p => p.y);

        // 重心から各点への角度でソート
        return points.OrderBy(p => Math.Atan2(p.y - centroidY, p.x - centroidX)).ToArray();
    }

    /// <summary>
    /// 最小面積外接矩形を計算する
    /// </summary>
    private static (double x, double y, double width, double height, double angle) CalculateMinimumAreaBoundingRectangle((double x, double y)[] points)
    {
        double minArea = double.MaxValue;
        double bestAngle = 0;
        double bestX = 0, bestY = 0, bestWidth = 0, bestHeight = 0;

        // 各辺の角度を試して最小面積の矩形を見つける
        for (int i = 0; i < points.Length; i++)
        {
            var (x1, y1) = points[i];
            var (x2, y2) = points[(i + 1) % points.Length];

            // この辺の角度を計算
            var edgeAngle = Math.Atan2(y2 - y1, x2 - x1);

            // この角度で全ての点を回転
            var rotatedPoints = points.Select(p => RotatePoint(p, -edgeAngle, 0, 0)).ToArray();

            // 回転後の点の軸平行外接矩形を計算
            var minX = rotatedPoints.Min(p => p.x);
            var maxX = rotatedPoints.Max(p => p.x);
            var minY = rotatedPoints.Min(p => p.y);
            var maxY = rotatedPoints.Max(p => p.y);

            var width = maxX - minX;
            var height = maxY - minY;
            var area = width * height;

            if (area < minArea)
            {
                minArea = area;
                bestAngle = edgeAngle * 180.0 / Math.PI;
                bestWidth = width;
                bestHeight = height;

                // 元の座標系での左上角を計算
                (bestX, bestY) = RotatePoint((minX, minY), edgeAngle, 0, 0);
            }
        }

        // 最終的な外接矩形の左上角を計算（簡単のため軸平行外接矩形を使用）
        var finalMinX = points.Min(p => p.x);
        var finalMinY = points.Min(p => p.y);

        return (finalMinX, finalMinY, bestWidth, bestHeight, bestAngle);
    }

    /// <summary>
    /// 点を指定した角度だけ回転する
    /// </summary>
    private static (double x, double y) RotatePoint((double x, double y) point, double angle, double centerX, double centerY)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        var dx = point.x - centerX;
        var dy = point.y - centerY;

        return (
            centerX + dx * cos - dy * sin,
            centerY + dx * sin + dy * cos
        );
    }
}