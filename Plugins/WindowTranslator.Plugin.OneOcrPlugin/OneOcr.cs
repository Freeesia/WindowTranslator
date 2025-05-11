using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules;
using WinRT;
using static WindowTranslator.Plugin.OneOcrPlugin.NativeMethods;

namespace WindowTranslator.Plugin.OneOcrPlugin;

[DisplayName("OneOcr文字認識")]
public class OneOcr : IOcrModule
{
    const string apiKey = "kj)TGtrK>f]b[Piow.gU+nC@s\"\"\"\"\"\"4";
    const int maxLineCount = 1000;
    private readonly ILogger<OneOcr> logger;
    private readonly long pipeline;
    private readonly long opt;
    private readonly long context;

    static OneOcr()
    {
        var context = AssemblyLoadContext.GetLoadContext(typeof(OneOcr).Assembly) ?? throw new InvalidOperationException();
        context.ResolvingUnmanagedDll += Context_ResolvingUnmanagedDll;
    }

    public OneOcr(ILogger<OneOcr> logger)
    {
        this.logger = logger;

        // OCR初期化オプションの作成
        var res = CreateOcrInitOptions(out this.context);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRの初期化オプション作成に失敗しました。エラーコード: {res}");
        }

        // モデル遅延読み込みの設定
        // 参考実装では0（無効）を設定
        res = OcrInitOptionsSetUseModelDelayLoad(this.context, 0);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRモデル遅延読み込み設定に失敗しました。エラーコード: {res}");
        }

        // OCRパイプラインを作成
        res = CreateOcrPipeline(Path.Combine(Utility.OneOcrPath, Utility.OneOcrModel), apiKey, context, out this.pipeline);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRパイプラインの作成に失敗しました。エラーコード: {res}");
        }

        // OCRプロセスオプション作成
        res = CreateOcrProcessOptions(out this.opt);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRプロセスオプションの作成に失敗しました。エラーコード: {res}");
        }

        // 最大認識行数を設定
        res = OcrProcessOptionsSetMaxRecognitionLineCount(opt, maxLineCount);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCR最大認識行数の設定に失敗しました。エラーコード: {res}");
        }
    }

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
        => await Task.Run(() => Recognize(bitmap)).ConfigureAwait(false);

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
            throw new InvalidOperationException($"OCRパイプラインの実行に失敗しました。エラーコード: {res}");
        }

        // 認識された行数を取得
        res = GetOcrLineCount(instance, out var lineCount);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCR行数の取得に失敗しました。エラーコード: {res}");
        }

        var textRects = new List<TextRect>((int)lineCount);

        // 各行の内容を処理
        for (long i = 0; i < lineCount; i++)
        {
            // 行を取得
            res = GetOcrLine(instance, i, out var line);
            if (res != 0 || line == 0)
            {
                throw new InvalidOperationException($"OCR行の取得に失敗しました。行番号: {i}, エラーコード: {res}");
            }

            // 行のテキスト内容を取得
            res = GetOcrLineContent(line, out var lineContent);
            if (res != 0)
            {
                throw new InvalidOperationException($"OCR行のテキスト内容の取得に失敗しました。行番号: {i}, エラーコード: {res}");
            }

            if (string.IsNullOrEmpty(lineContent))
            {
                continue;
            }

            // 境界ボックスを取得
            res = GetOcrLineBoundingBox(line, out var ptr);
            if (res != 0)
            {
                throw new InvalidOperationException($"OCR行の境界ボックスの取得に失敗しました。行番号: {i}, エラーコード: {res}");
            }
            var boundingBox = Marshal.PtrToStructure<BoundingBox>(ptr);

            // 境界ボックスから座標を計算
            var left = Math.Min(Math.Min(boundingBox.x1, boundingBox.x2), Math.Min(boundingBox.x3, boundingBox.x4));
            var top = Math.Min(Math.Min(boundingBox.y1, boundingBox.y2), Math.Min(boundingBox.y3, boundingBox.y4));
            var right = Math.Max(Math.Max(boundingBox.x1, boundingBox.x2), Math.Max(boundingBox.x3, boundingBox.x4));
            var bottom = Math.Max(Math.Max(boundingBox.y1, boundingBox.y2), Math.Max(boundingBox.y3, boundingBox.y4));

            var width = right - left;
            var height = bottom - top;

            // TextRectを作成して追加
            textRects.Add(new TextRect(lineContent, left, top, width, height, height, false));
        }

        this.logger.LogDebug($"Recognize: {sw.Elapsed}");
        return textRects;
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
}

[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}

public class OneOcrValidator : ITargetSettingsValidator
{
    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(IOcrModule)] != nameof(OneOcr))
        {
            return ValidateResult.Valid;
        }

        if (!Utility.NeedCopyDll())
        {
            return ValidateResult.Valid;
        }

        // OneOcrのインストール先を取得
        var oneOcrPath = await Utility.FindOneOcrPath().ConfigureAwait(false);
        if (oneOcrPath == null)
        {
            return ValidateResult.Invalid("OneOcr", "依存モジュールが見つかりません。この環境では利用できません。");
        }

        // DLLをコピー
        try
        {
            Utility.CopyDll(oneOcrPath);
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid("OneOcr", $"OneOcrのDLLのコピーに失敗しました。{ex.Message}");
        }

        return ValidateResult.Valid;
    }
}