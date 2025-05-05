using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.OneOcrPlugin;

[DisplayName("OneOcr文字認識")]
public class OneOcr : IOcrModule, IDisposable
{
    private readonly ILogger<OneOcr> logger;
    private readonly string modelPath = "oneocr.onemodel";
    private readonly string apiKey = "kj)TGtrK>f]b[Piow.gU+nC@s\"\"\"\"\"\"4";
    private readonly int maxLineCount = 1000;
    private long context;
    private bool disposed;

    public OneOcr(ILogger<OneOcr> logger)
    {
        this.logger = logger;
        
        // OCR初期化オプションの作成
        long res = NativeMethods.CreateOcrInitOptions(out long ctx);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRの初期化オプション作成に失敗しました。エラーコード: {res}");
        }
        context = ctx;

        // モデル遅延読み込みの設定
        // 参考実装では0（無効）を設定
        res = NativeMethods.OcrInitOptionsSetUseModelDelayLoad(ctx, 0);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRモデル遅延読み込み設定に失敗しました。エラーコード: {res}");
        }
        logger.LogInformation("OneOcr初期化に成功しました");
    }

    public ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        // モデルキーとパス
        string modelPath = this.modelPath;
        string key = this.apiKey;

        // BitmapをImgに変換
        var img = ConvertSoftwareBitmapToImg(bitmap);

        // OCRパイプラインを作成
        long res = NativeMethods.CreateOcrPipeline(modelPath, key, context, out long pipeline);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRパイプラインの作成に失敗しました。エラーコード: {res}");
        }

        // OCRプロセスオプション作成
        res = NativeMethods.CreateOcrProcessOptions(out long opt);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRプロセスオプションの作成に失敗しました。エラーコード: {res}");
        }

        // 最大認識行数を設定
        res = NativeMethods.OcrProcessOptionsSetMaxRecognitionLineCount(opt, maxLineCount);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCR最大認識行数の設定に失敗しました。エラーコード: {res}");
        }

        // OCRパイプラインを実行
        res = NativeMethods.RunOcrPipeline(pipeline, ref img, opt, out long instance);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCRパイプラインの実行に失敗しました。エラーコード: {res}");
        }

        // 認識された行数を取得
        res = NativeMethods.GetOcrLineCount(instance, out long lineCount);
        if (res != 0)
        {
            throw new InvalidOperationException($"OCR行数の取得に失敗しました。エラーコード: {res}");
        }

        List<TextRect> textRects = new List<TextRect>();

        // 各行の内容を処理
        for (long i = 0; i < lineCount; i++)
        {
            // 行を取得
            res = NativeMethods.GetOcrLine(instance, i, out long line);
            if (res != 0 || line == 0)
            {
                throw new InvalidOperationException($"OCR行の取得に失敗しました。行番号: {i}, エラーコード: {res}");
            }

            // 行のテキスト内容を取得
            res = NativeMethods.GetOcrLineContent(line, out IntPtr lineContentPtr);
            if (res != 0)
            {
                throw new InvalidOperationException($"OCR行のテキスト内容の取得に失敗しました。行番号: {i}, エラーコード: {res}");
            }

            var lineContent = Marshal.PtrToStringUTF8(lineContentPtr);
            if (string.IsNullOrEmpty(lineContent))
            {
                continue;
            }

            // 境界ボックスを取得
            res = NativeMethods.GetOcrLineBoundingBox(line, out var boundingBox);
            if (res != 0)
            {
                throw new InvalidOperationException($"OCR行の境界ボックスの取得に失敗しました。行番号: {i}, エラーコード: {res}");
            }

            // 境界ボックスから座標を計算
            float left = Math.Min(Math.Min(boundingBox.x1, boundingBox.x2), Math.Min(boundingBox.x3, boundingBox.x4));
            float top = Math.Min(Math.Min(boundingBox.y1, boundingBox.y2), Math.Min(boundingBox.y3, boundingBox.y4));
            float right = Math.Max(Math.Max(boundingBox.x1, boundingBox.x2), Math.Max(boundingBox.x3, boundingBox.x4));
            float bottom = Math.Max(Math.Max(boundingBox.y1, boundingBox.y2), Math.Max(boundingBox.y3, boundingBox.y4));

            float width = right - left;
            float height = bottom - top;

            // フォントサイズは高さから推定
            double fontSize = height;

            // TextRectを作成して追加
            textRects.Add(new TextRect(lineContent, left, top, width, height, fontSize, false));
        }

        return new ValueTask<IEnumerable<TextRect>>(textRects);
    }

    private unsafe Img ConvertSoftwareBitmapToImg(SoftwareBitmap bitmap)
    {
        BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
        IMemoryBufferReference reference = buffer.CreateReference();

        try
        {
            // バッファへのアクセスを取得
            byte* dataInBytes;
            uint capacity;
            ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacity);

            // 直接Img構造体を作成して返す
            return new Img(
                T: 16, // CV_8UC4 (BGRA)
                Col: bitmap.PixelWidth,
                Row: bitmap.PixelHeight,
                Unk: 0,
                Step: buffer.GetPlaneDescription(0).Stride,
                Data: (IntPtr)dataInBytes
            );
        }
        finally
        {
            // referenceとbufferはIDiposableを実装しているため、明示的に解放
            reference?.Dispose();
            buffer?.Dispose();
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            // コンテキストのクリーンアップが必要ならここで行う
            disposed = true;
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
