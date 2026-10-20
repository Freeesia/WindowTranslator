using WindowTranslator.Plugin.OneOcrPlugin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Panlingo.LanguageIdentification.FastText;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.OneOcrPlugin.Properties;
using WinRT;
using static WindowTranslator.LanguageUtility;
using static WindowTranslator.OcrUtility;
using static WindowTranslator.Plugin.OneOcrPlugin.NativeMethods;

namespace WindowTranslator.Plugin.BluePrinceOcrPlugin;

[System.ComponentModel.DisplayName("BluPrinceOcr")]
public sealed class BluPrinceOcr : IOcrModule, IDisposable
{
    const string apiKey = "kj)TGtrK>f]b[Piow.gU+nC@s\"\"\"\"\"\"4";
    const int maxLineCount = 1000;
    private readonly FastTextDetector? fastText;
    private readonly ILogger<BluPrinceOcr> logger;
    private readonly string source;
    private readonly HashSet<string> targets;
    private readonly long pipeline;
    private readonly long opt;
    private readonly long context;
    private readonly double scale = 1.0; // スケールのデフォルト値
    private readonly int brightness; // 明るさ（-127 - 128）
    private readonly int contrast; // コントラスト（-99 - 100）

    static BluPrinceOcr()
    {
        var context = AssemblyLoadContext.GetLoadContext(typeof(BluPrinceOcr).Assembly) ?? throw new InvalidOperationException();
        context.ResolvingUnmanagedDll += Context_ResolvingUnmanagedDll;
    }

    private static nint Context_ResolvingUnmanagedDll(Assembly assembly, string arg2)
    {
        var fullPath = Path.Combine(Utility.OneOcrPath, arg2);
        if (File.Exists(fullPath))
        {
            return NativeLibrary.Load(fullPath);
        }
        else
        {
            return 0;
        }
    }

    public BluPrinceOcr(ILogger<BluPrinceOcr> logger, IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<BasicOcrParam> ocrParam)
    {
        this.logger = logger;
        this.fastText = FastTextDetector.IsSupported() ? new() : null;
        this.fastText?.LoadDefaultModel();
        this.source = langOptions.Value.Source;
        this.targets = [langOptions.Value.Target[..2]];
        if (this.targets.Overlaps(["ja", "zh"]) && this.source[..2] is not "ja" and not "zh")
        {
            // 日本語と中国語の判定は難しいので、お互いの翻訳でないときは、どちらも翻訳対象外となる翻訳先言語とする
            this.targets.UnionWith(["ja", "zh"]);
        }
        // 閾値パラメータの設定
        this.scale = ocrParam.Value.Scale;
        this.brightness = ocrParam.Value.Brightness;
        this.contrast = ocrParam.Value.Contrast;

        // OCR初期化オプションの作成
        var res = CreateOcrInitOptions(out this.context);
        if (res != 0)
        {
            throw new InvalidOperationException(string.Format(Resources.InitFaild, res));
        }

        // モデル遅延読み込みの設定
        // 参考実装では0（無効）を設定
        res = OcrInitOptionsSetUseModelDelayLoad(this.context, 0);
        if (res != 0)
        {
            throw new InvalidOperationException(string.Format(Resources.SetLazyLoadFaild, res));
        }

        // OCRパイプラインを作成
        res = CreateOcrPipeline(Path.Combine(Utility.OneOcrPath, Utility.OneOcrModel), apiKey, context, out this.pipeline);
        if (res != 0)
        {
            File.WriteAllText(Path.Combine(Utility.OneOcrPath, Utility.ErrorPath),
                $"""
                OCRパイプラインの作成に失敗しました。エラーコード: {res}
                """);
            throw new InvalidOperationException(string.Format(Resources.CreatePipelineFaild, res));
        }

        // OCRプロセスオプション作成
        res = CreateOcrProcessOptions(out this.opt);
        if (res != 0)
        {
            throw new InvalidOperationException(string.Format(Resources.CreateProcessOptionsFaild, res));
        }

        // 最大認識行数を設定
        res = OcrProcessOptionsSetMaxRecognitionLineCount(opt, maxLineCount);
        if (res != 0)
        {
            throw new InvalidOperationException(string.Format(Resources.SetMaxRecognitionLineCountFaild, res));
        }
    }

    public void Dispose()
    {
        this.fastText?.Dispose();
    }

    public ValueTask<IReadOnlyList<TextRect>> RecognizeAsync(OcrCaptureInput input)
        => OcrUtility.RecognizeRegionsAsync(
            input,
            RecognizeRegionAsync,
            this.scale,
            this.brightness,
            this.contrast);

    /// <summary>
    /// 指定した画像のテキストを認識する
    /// </summary>
    /// <param name="workingBitmap">認識対象の画像</param>
    /// <param name="sourceSize">拡大後の全体画像サイズ</param>
    private async ValueTask<IReadOnlyList<TextRect>> RecognizeRegionAsync(
        SoftwareBitmap workingBitmap,
        System.Drawing.Size sourceSize)
    {

        // テキスト認識処理をバックグラウンドで実行
        var textRects = await Task.Run(() => Recognize(workingBitmap)).ConfigureAwait(false);

        // 認識したテキスト矩形の補正と結合処理を実行
        textRects = ProcessTextRects(textRects, sourceSize.Width, sourceSize.Height);

        var wFat = sourceSize.Width * 0.004;

        return textRects
            // マージ後に少なすぎる文字も認識ミス扱い
            // (特殊なグリフの言語は対象外(日本語、中国語、韓国語、ロシア語))
            .Where(r => IsSpecialLang(this.source) || r.SourceText.Length > 2)
            // 全部数字なら対象外
            .Where(r => !AllSymbolOrSpace().IsMatch(r.SourceText))
            // 翻訳後言語のテキストは対象外
            .Where(r => !IsTargetLangText(r.SourceText))
            // 若干太らせる
            .Select(r => r with { X = r.X - wFat, Width = r.Width + (wFat * 2), Y = r.Y - (r.Height * 0.04), Height = r.Height * 1.08 })
            .ToArray();
    }

    private bool IsTargetLangText(string text)
        => this.fastText?.Predict(text, 3, 0.7f).Any(p => this.targets.Contains(p.Label[(p.Label.LastIndexOf('_') + 1)..]))
            // fastTextがサポートされていない場合は無条件で対象外
            ?? false;

    private unsafe IEnumerable<TextRect> Recognize(SoftwareBitmap bitmap)
    {
        var sw = Stopwatch.StartNew();
        // BitmapをImgに変換
        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        using var reference = buffer.CreateReference();
        reference.As<IMemoryBufferByteAccess>().GetBuffer(out var data, out _);
        var img = new Img(
            T: 3, // (BGRA)
            Col: bitmap.PixelWidth,
            Row: bitmap.PixelHeight,
            Unk: 0,
            Step: buffer.GetPlaneDescription(0).Stride,
            Data: (IntPtr)data
        );
        long res;

        // OCRパイプラインを実行
        res = RunOcrPipeline(pipeline, ref img, opt, out var instance);
        if (res != 0)
        {
            if (res == 3)
            {
                this.logger.LogDebug("画像が小さすぎるので認識出来なかった");
                return [];
            }
            throw new InvalidOperationException(string.Format(Resources.RunPipelineFaild, res));
        }

        // 認識された行数を取得
        res = GetOcrLineCount(instance, out var lineCount);
        if (res != 0)
        {
            throw new InvalidOperationException(string.Format(Resources.GetLineCountFaild, res));
        }

        var textRects = new List<TextRect>((int)lineCount);

        // 各行の内容を処理
        for (long i = 0; i < lineCount; i++)
        {
            // 行を取得
            res = GetOcrLine(instance, i, out var line);
            if (res != 0 || line == 0)
            {
                throw new InvalidOperationException(string.Format(Resources.GetLineFaild, i, res));
            }

            // 行のテキスト内容を取得
            res = GetOcrLineContent(line, out var lineContent);
            if (res != 0)
            {
                throw new InvalidOperationException(string.Format(Resources.GetLineContentFaild, i, res));
            }

            if (string.IsNullOrEmpty(lineContent))
            {
                continue;
            }

            // 境界ボックスを取得
            res = GetOcrLineBoundingBox(line, out var ptr);
            if (res != 0)
            {
                throw new InvalidOperationException(string.Format(Resources.GetLineBoundingBoxFaild, i, res));
            }
            var boundingBox = Marshal.PtrToStructure<BoundingBox>(ptr);
            // 傾いた矩形の適切なサイズと位置を計算
            var (x, y, width, height, angle) = LineGeometry.Calculate(boundingBox);

            // デバッグ情報をログに出力
            this.logger.LogDebug($"Text: '{lineContent}', Angle: {angle:F2}°, Size: {width:F1}x{height:F1}, Position: ({x:F1}, {y:F1})");

            // TextRectを作成して追加（角度情報を含む、座標は左上位置）
            textRects.Add(new(lineContent, x, y, width, height, height * 0.9, false) { Angle = angle });
        }

        this.logger.LogDebug($"Recognize: {sw.Elapsed}");
        return textRects;
    }

    /// <summary>
    /// 認識したテキスト矩形の補正と結合を行う
    /// </summary>
    /// <param name="textRects">認識したテキスト矩形のリスト</param>
    /// <param name="imageWidth">画像の幅</param>
    /// <param name="imageHeight">画像の高さ</param>
    /// <returns>補正・結合後のテキスト矩形のリスト</returns>
    private IEnumerable<TextRect> ProcessTextRects(IEnumerable<TextRect> textRects, int imageWidth, int imageHeight)
        => ParagraphBuilder.Build(textRects);
}
[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}




