using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace WindowTranslator.Plugin.OneOcrPlugin;

static class Utility
{
    public const double IgnoreAngleThreshold = 3.0; // 度
    private const string OneOcrDll = "oneocr.dll";
    public const string OneOcrModel = "oneocr.onemodel";
    private const string OnnxRuntimeDll = "onnxruntime.dll";
    public const string ErrorPath = "Error.txt";
    public const string ScreenSketchAppName = "Microsoft.ScreenSketch";
    public const string ScreenSketchProductId = "9MZ95KL8MR0L";

    // 動作するScreenSketchのバージョン
    private static readonly Dictionary<int, Version> MinimumVersions = new()
    {
        [10] = new("10.2008.3001.0"),  // Win10
        [11] = new("11.2508.29.0"),    // Win11
    };

    public static string OneOcrPath { get; } = Path.Combine(PathUtility.SharedDir, "OneOcr");

    private static int GetOsVersion()
        => Environment.OSVersion.Version is { Major: 10, Build: < 22000 } ? 10 : 11;

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
        path = path.Trim();
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        return path;
    }

    /// <summary>
    /// Get-AppxPackageを使用してScreenSketchのバージョンを取得する
    /// </summary>
    /// <returns>バージョン文字列。見つからない場合はnull</returns>
    private static async ValueTask<Version?> GetInstallVersion(string appName)
    {
        var info = new ProcessStartInfo("powershell.exe", $"-Command \"(Get-AppxPackage -Name {appName}).Version\"")
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
        var version = await p.StandardOutput.ReadToEndAsync();
        version = version.Trim();
        if (string.IsNullOrEmpty(version))
        {
            return null;
        }
        if (!Version.TryParse(version, out var v))
        {
            throw new InvalidOperationException($"Failed to parse {appName} version. : {version}");
        }
        return v;
    }

    public static async ValueTask<string?> FindOneOcrPath()
    {
        var scketch = await GetInstallLocation(ScreenSketchAppName).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(scketch))
        {
            var path = Path.Combine(scketch, "SnippingTool");
            if (File.Exists(Path.Combine(path, OneOcrDll)))
            {
                return path;
            }
        }
        return null;
    }

    /// <summary>
    /// ScreenSketchのバージョンをチェックし、必要に応じてMicrosoft Storeを開く
    /// </summary>
    /// <returns>バージョンが十分か、または更新が必要かを示す情報</returns>
    public static async ValueTask<bool> CheckScreenSketchVersionAsync(ILogger logger)
    {
        // インストール済みのScreenSketchバージョンを取得
        var installedVersion = await GetInstallVersion(ScreenSketchAppName).ConfigureAwait(false);

        if (installedVersion == null)
        {
            logger.LogWarning("ScreenSketch is not installed.");
            // ScreenSketchがインストールされていない
            return false;
        }

        // 必要な最小バージョンを取得
        if (!MinimumVersions.TryGetValue(GetOsVersion(), out var minVersion))
        {
            logger.LogWarning("Unsupported OS version: {OSVersion}", Environment.OSVersion.Version);
            // サポートされていないOSバージョン（OSバージョンに応じたチェックをスキップ）
            return false;
        }

        // バージョン比較
        if (installedVersion >= minVersion)
        {
            // バージョンが十分新しい
            return true;
        }

        logger.LogInformation("ScreenSketch version is outdated. Installed: {InstalledVersion}, Required: {RequiredVersion}", installedVersion, minVersion);
        // バージョンが古い
        return false;
    }

    /// <summary>
    /// Microsoft Storeを開いてScreenSketchの更新を促す
    /// </summary>
    public static void OpenStoreForUpdate()
    {
        Process.Start(new ProcessStartInfo($"ms-windows-store://pdp/?ProductId={ScreenSketchProductId}")
        {
            UseShellExecute = true
        });
    }

    public static bool NeedCopyDll()
    {
        if (File.Exists(Path.Combine(OneOcrPath, ErrorPath)))
        {
            return true;
        }
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
        File.Delete(Path.Combine(OneOcrPath, ErrorPath));
        File.Copy(Path.Combine(path, OneOcrDll), Path.Combine(OneOcrPath, OneOcrDll), true);
        File.Copy(Path.Combine(path, OneOcrModel), Path.Combine(OneOcrPath, OneOcrModel), true);
        File.Copy(Path.Combine(path, OnnxRuntimeDll), Path.Combine(OneOcrPath, OnnxRuntimeDll), true);
    }

    /// <summary>
    /// 傾いた矩形（4つの頂点）から適切な位置、サイズ、角度を計算する
    /// OneOcrの仕様: (x1,y1)と(x2,y2)を結ぶ線が矩形の上端
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

        // (x1,y1)(x2,y2)間と(x2,y2)(x3,y3)間の距離を計算
        var distance12 = Math.Sqrt(Math.Pow(boundingBox.x2 - boundingBox.x1, 2) + Math.Pow(boundingBox.y2 - boundingBox.y1, 2));
        var distance23 = Math.Sqrt(Math.Pow(boundingBox.x3 - boundingBox.x2, 2) + Math.Pow(boundingBox.y3 - boundingBox.y2, 2));

        // 距離の長い方を矩形の上端として角度を計算
        double edgeAngle;
        if (distance12 >= distance23)
        {
            // (x1,y1)と(x2,y2)を結ぶ線が長い場合
            edgeAngle = Math.Atan2(boundingBox.y2 - boundingBox.y1, boundingBox.x2 - boundingBox.x1);
        }
        else
        {
            // (x2,y2)と(x3,y3)を結ぶ線が長い場合
            edgeAngle = Math.Atan2(boundingBox.y3 - boundingBox.y2, boundingBox.x3 - boundingBox.x2);
        }

        var angleInDegrees = edgeAngle * 180.0 / Math.PI;

        // 角度が閾値未満なら角度を無視する
        if (Math.Abs(angleInDegrees) < IgnoreAngleThreshold)
        {
            // 単純に外接矩形を計算
            var simpleMinX = points.Min(p => p.x);
            var simpleMaxX = points.Max(p => p.x);
            var simpleMinY = points.Min(p => p.y);
            var simpleMaxY = points.Max(p => p.y);

            var simpleWidth = simpleMaxX - simpleMinX;
            var simpleHeight = simpleMaxY - simpleMinY;

            // 角度を0度として返す
            return (simpleMinX, simpleMinY, simpleWidth, simpleHeight, 0.0);
        }

        // この角度で全ての点を回転
        var rotatedPoints = points.Select(p => RotatePoint(p, -edgeAngle, 0, 0)).ToArray();

        // 回転後の点の軸平行外接矩形を計算
        var minX = rotatedPoints.Min(p => p.x);
        var maxX = rotatedPoints.Max(p => p.x);
        var minY = rotatedPoints.Min(p => p.y);
        var maxY = rotatedPoints.Max(p => p.y);

        var width = maxX - minX;
        var height = maxY - minY;

        // 回転後の左上角を元の座標系に戻す
        var (x, y) = RotatePoint((minX, minY), edgeAngle, 0, 0);

        return (x, y, width, height, angleInDegrees);
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

    private static void MigrateModelsIfNeeded()
    {
        var oldPath = Path.Combine(PathUtility.UserDir, "OneOcr");
        if (Directory.Exists(oldPath) && !Directory.Exists(OneOcrPath))
        {
            Directory.CreateDirectory(PathUtility.SharedDir);
            Directory.Move(oldPath, OneOcrPath);
        }
    }

    [ModuleInitializer]
    internal static void Initialize()
    {
        MigrateModelsIfNeeded();
    }
}