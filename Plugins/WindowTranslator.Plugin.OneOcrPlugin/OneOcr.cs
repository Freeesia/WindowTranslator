using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Panlingo.LanguageIdentification.FastText;
using Windows.Graphics.Imaging;
using WindowTranslator.Collections;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.OneOcrPlugin.Properties;
using WinRT;
using static WindowTranslator.LanguageUtility;
using static WindowTranslator.OcrUtility;
using static WindowTranslator.Plugin.OneOcrPlugin.NativeMethods;

namespace WindowTranslator.Plugin.OneOcrPlugin;

[DefaultModule]
public sealed class OneOcr : IOcrModule, IDisposable
{
    const string apiKey = "kj)TGtrK>f]b[Piow.gU+nC@s\"\"\"\"\"\"4";
    const int maxLineCount = 1000;
    private readonly FastTextDetector fastText;
    private readonly ILogger<OneOcr> logger;
    private readonly string source;
    private readonly HashSet<string> targets;
    private readonly long pipeline;
    private readonly long opt;
    private readonly long context;
    private readonly double xPosThrethold;
    private readonly double yPosThrethold;
    private readonly double leadingThrethold;
    private readonly double spacingThreshold;
    private readonly double fontSizeThrethold;
    private readonly bool isAvoidMergeList;
    private readonly double scale = 1.0; // スケールのデフォルト値
    private readonly List<PriorityRect> priorityRects;

    static OneOcr()
    {
        var context = AssemblyLoadContext.GetLoadContext(typeof(OneOcr).Assembly) ?? throw new InvalidOperationException();
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

    public OneOcr(ILogger<OneOcr> logger, IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<BasicOcrParam> ocrParam)
    {
        this.logger = logger;
        this.fastText = new FastTextDetector();
        this.fastText.LoadDefaultModel();
        this.source = langOptions.Value.Source;
        this.targets = [langOptions.Value.Target[..2]];
        if (this.targets.Overlaps(["ja", "zh"]) && this.source[..2] is not "ja" and not "zh")
        {
            // 日本語と中国語の判定は難しいので、お互いの翻訳でないときは、どちらも翻訳対象外となる翻訳先言語とする
            this.targets.UnionWith(["ja", "zh"]);
        }
        // 閾値パラメータの設定
        this.xPosThrethold = ocrParam.Value.XPosThrethold;
        this.yPosThrethold = ocrParam.Value.YPosThrethold;
        this.leadingThrethold = ocrParam.Value.LeadingThrethold;
        this.spacingThreshold = ocrParam.Value.SpacingThreshold;
        this.fontSizeThrethold = ocrParam.Value.FontSizeThrethold;
        this.isAvoidMergeList = ocrParam.Value.IsAvoidMergeList;
        this.scale = ocrParam.Value.Scale;
        this.priorityRects = ocrParam.Value.PriorityRects ?? [];

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
        this.fastText.Dispose();
    }

    public async ValueTask<IEnumerable<TextRect>> RecognizeAsync(SoftwareBitmap bitmap)
    {
        // 優先矩形が指定されている場合は、それらのみを認識
        if (this.priorityRects.Count > 0)
        {
            return await RecognizePriorityRectsAsync(bitmap);
        }

        // 優先矩形がない場合は通常の全体認識
        return await RecognizeFullScreenAsync(bitmap);
    }

    private async ValueTask<IEnumerable<TextRect>> RecognizePriorityRectsAsync(SoftwareBitmap bitmap)
    {
        var allResults = new List<TextRect>();

        foreach (var priorityRect in this.priorityRects)
        {
            // 元の画像サイズで絶対座標を計算
            var absRect = priorityRect.ToAbsoluteRect(bitmap.PixelWidth, bitmap.PixelHeight);

            // 元の画像から矩形を切り出し
            using var croppedBitmap = bitmap.Crop(absRect);
            
            // 切り出した画像をスケーリング
            using var scaledCroppedBitmap = await croppedBitmap.ResizeSoftwareBitmapAsync(this.scale);
            
            // スケーリングされた切り出し画像をOCR
            var rectResults = await RecognizeRegionAsync(scaledCroppedBitmap);

            // 座標をスケール変換して元の画像座標系に変換
            // RecognizeRegionAsyncの結果はスケール済み画像の座標なので、スケールで割る
            allResults.AddRange(rectResults.Select(text => 
                new TextRect(
                    text.SourceText,
                    text.X / this.scale + absRect.X,
                    text.Y / this.scale + absRect.Y,
                    text.Width / this.scale,
                    text.Height / this.scale,
                    text.FontSize / this.scale,
                    text.MultiLine,
                    text.Foreground,
                    text.Background
                ) { Context = priorityRect.Keyword }));
        }

        return allResults;
    }

    private async ValueTask<IEnumerable<TextRect>> RecognizeFullScreenAsync(SoftwareBitmap bitmap)
    {
        // 拡大率に基づくリサイズ処理
        var workingBitmap = await bitmap.ResizeSoftwareBitmapAsync(this.scale);

        var results = await RecognizeRegionAsync(workingBitmap);

        if (workingBitmap != bitmap)
        {
            workingBitmap.Dispose();
        }

        return results;
    }

    private async ValueTask<IEnumerable<TextRect>> RecognizeRegionAsync(SoftwareBitmap workingBitmap)
    {
        // テキスト認識処理をバックグラウンドで実行
        var textRects = await Task.Run(() => Recognize(workingBitmap)).ConfigureAwait(false);

        // 認識したテキスト矩形の補正と結合処理を実行
        textRects = ProcessTextRects(textRects, workingBitmap.PixelWidth, workingBitmap.PixelHeight);

        var wFat = workingBitmap.PixelWidth * 0.004;

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
        => this.fastText.Predict(text, 3, 0.7f).Any(p => this.targets.Contains(p.Label[(p.Label.LastIndexOf('_') + 1)..]));

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
            var (x, y, width, height, angle) = Utility.CalculateOrientedRect(boundingBox);

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
    {
        var sw = Stopwatch.StartNew();

        // 空のリストや無効なテキストの場合は処理しない
        var rects = textRects.Where(r => !string.IsNullOrEmpty(r.SourceText)).ToArray();
        if (rects.Length == 0)
        {
            return Array.Empty<TextRect>();
        }

        // 閾値の計算
        var xt = xPosThrethold * imageWidth;
        var yt = yPosThrethold * imageHeight;

        // 結果格納用リスト
        var results = new List<TextRectMerger>(rects.Length);

        // Y座標でソートしたRemovableQueueを作成（上から下へ処理するため）
        var queue = new RemovableQueue<TextRect>(rects.OrderBy(r => r.Y));

        while (queue.TryDequeue(out var target))
        {
            // マージ用の一時オブジェクトを作成
            var temp = new TextRectMerger(target);
            var merged = false;

            // マージできる限りマージを続ける
            do
            {
                merged = false;
                foreach (var rect in queue.ToList())
                {
                    if (CanMerge(temp, rect, xt, yt))
                    {
                        temp.Merge(rect);
                        queue.Remove(rect);
                        merged = true;
                    }
                }
            } while (merged);

            // 結果に追加
            results.Add(temp);
        }

        this.logger.LogDebug($"ProcessTextRects: {sw.Elapsed}");

        return results.Select(ToTextRect);
    }

    /// <summary>
    /// 矩形同士が結合可能かどうかを判断する
    /// </summary>
    private bool CanMerge(TextRectMerger temp, TextRect rect, double xThreshold, double yThreshold)
    {
        // 角度の差が閾値以上の場合はマージしない
        var angleDiff = Math.Abs(temp.Rects.Average(r => r.Angle) - rect.Angle);
        if (angleDiff >= Utility.IgnoreAngleThreshold)
        {
            return false;
        }

        // 重なっている場合はマージできる
        if (temp.IntersectsWith(rect))
        {
            return true;
        }

        // フォントサイズが大きく異なる場合はマージできない
        var fDiff = Math.Abs(temp.FontSize - rect.FontSize) / Math.Min(temp.FontSize, rect.FontSize);
        if (fDiff > fontSizeThrethold)
        {
            return false;
        }

        var fontSize = (temp.FontSize + rect.FontSize) / 2;
        var (text, x, y, w, _, _, _, _, _) = rect;

        // y座標が近く、x間隔が近い場合にマージできる（横方向の結合）
        var xGap = Math.Min(Math.Abs((temp.X + temp.Width) - x), Math.Abs((x + w) - temp.X)); // X座標の間隔
        var yDiff = Math.Abs(temp.Y - y); // Y座標の差
        var gThre = fontSize * spacingThreshold;
        if (xGap < gThre && yDiff < yThreshold)
        {
            return true;
        }

        // マージ元先の文字列が両方単語数が少ない場合は縦にマージできない
        // 言語によって判定方法を変える（スペース区切りの言語と非スペース区切りの言語で異なる）
        if (this.isAvoidMergeList && (IsSpaceLang(this.source) ?
            (WordCount(temp.Text) <= 2 && WordCount(text) <= 2) :
            (temp.Text.Length <= 8 && text.Length <= 8)))
        {
            return false;
        }

        // x座標が近く、y間隔が近い場合にマージできる（縦方向の結合）
        // ただし、rectのwidthがtempのwidthよりも2倍以上大きい場合は除外
        var xDiff = Math.Abs(temp.X - x); // X座標の差
        var yGap = Math.Abs((temp.Y + temp.Height) - y); // Y座標の間隔
        var lThre = fontSize * leadingThrethold; // 行間の閾値
        if (xDiff < xThreshold && yGap < lThre && w < temp.Width * 2)
        {
            return true;
        }

        // x座標の中心が近く、y間隔が近い場合にマージできる（縦方向の中央揃え結合）
        var xCenter2 = x + (w * .5);
        var xCenterDiff = Math.Abs(temp.CenterX - xCenter2); // X座標の中心の差
        if (xCenterDiff < xThreshold && yGap < lThre)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 結合した矩形からTextRectを作成する
    /// </summary>
    private TextRect ToTextRect(TextRectMerger mergedRect)
    {
        var (x, y, width, height, fontSize, text) = mergedRect;

        // スケールに応じた座標変換
        if (this.scale != 1.0)
        {
            x /= scale;
            y /= scale;
            width /= scale;
            height /= scale;
            fontSize /= scale;
        }

        // 高さがフォントサイズの2倍以上の場合は複数行とみなす
        var lines = height / fontSize >= 2;

        // 結合された矩形の平均角度を計算
        var angle = mergedRect.Rects.Average(r => r.Angle);

        return new(text, x, y, width, height, fontSize, lines) { Angle = angle };
    }

    /// <summary>
    /// テキスト矩形の結合を行うためのヘルパークラス
    /// </summary>
    private class TextRectMerger
    {
        private readonly List<TextRect> rects;

        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public double FontSize { get; private set; }

        public double Left => X;
        public double Top => Y;
        public double Right => X + Width;
        public double Bottom => Y + Height;
        public double CenterX => X + (Width * 0.5);
        public IReadOnlyList<TextRect> Rects => this.rects;

        public string Text
        {
            get
            {
                var builder = new StringBuilder(this.Rects.Sum(r => r.SourceText.Length + 1));

                // Y座標でソートしてから、同じY座標内ではX座標でソート
                foreach (var rect in this.Rects
                    .OrderBy(r => (int)((r.Y - this.Y) / this.FontSize))
                    .ThenBy(r => r.X))
                {
                    builder.Append(rect.SourceText);
                    builder.Append(' ');
                }

                // 末尾の余分なスペースを削除
                if (builder.Length > 0)
                {
                    builder.Length--;
                }

                return builder.ToString();
            }
        }

        public TextRectMerger(TextRect rect)
        {
            (_, X, Y, Width, Height, FontSize, _, _, _) = rect;
            this.rects = [rect];
        }

        public void Merge(TextRect rect)
        {
            var (_, x, y, width, height, _, _, _, _) = rect;
            this.rects.Add(rect);

            // 新しい境界を計算
            var x1 = Math.Min(X, x);
            var y1 = Math.Min(Y, y);
            var x2 = Math.Max(X + Width, x + width);
            var y2 = Math.Max(Y + Height, y + height);

            // 境界を更新
            (X, Y, Width, Height) = (x1, y1, x2 - x1, y2 - y1);

            // フォントサイズは平均を取る
            FontSize = Rects.Average(r => r.FontSize);
        }

        public bool IntersectsWith(TextRect rect)
            => (rect.X < X + Width) && (X < rect.X + rect.Width) &&
               (rect.Y < Y + Height) && (Y < rect.Y + rect.Height);

        public void Deconstruct(out double x, out double y, out double width, out double height, out double fontSize, out string text)
        {
            x = X;
            y = Y;
            width = Width;
            height = Height;
            fontSize = FontSize;
            text = Text;
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
