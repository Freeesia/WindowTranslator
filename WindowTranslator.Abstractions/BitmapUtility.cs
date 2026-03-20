#if WINDOWS
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace WindowTranslator;

/// <summary>
/// 画像関連のユーティリティクラス
/// </summary>
public static class BitmapUtility
{
    private static readonly AsyncLocal<InMemoryRandomAccessStream> streamCache = new();

    /// <summary>
    /// 画像のリサイズを行う
    /// </summary>
    /// <param name="source">元画像</param>
    /// <param name="scale">拡大率</param>
    /// <param name="token">キャンセルトークン</param>
    /// <returns>リサイズ後の画像</returns>
    public static async ValueTask<SoftwareBitmap> ResizeSoftwareBitmapAsync(this SoftwareBitmap source, double scale, CancellationToken token = default)
    {
        var newWidth = (uint)(source.PixelWidth * scale);
        var newHeight = (uint)(source.PixelHeight * scale);

        if (newWidth == source.PixelWidth && newHeight == source.PixelHeight)
        {
            return source;
        }

        var resizeStream = streamCache.Value ??= new();

        resizeStream.Seek(0);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, resizeStream);
        token.ThrowIfCancellationRequested();
        encoder.SetSoftwareBitmap(source);
        encoder.BitmapTransform.InterpolationMode = scale > 1 ? BitmapInterpolationMode.Cubic : BitmapInterpolationMode.Fant;
        encoder.BitmapTransform.ScaledWidth = newWidth;
        encoder.BitmapTransform.ScaledHeight = newHeight;
        await encoder.FlushAsync();
        token.ThrowIfCancellationRequested();
        resizeStream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(resizeStream);
        token.ThrowIfCancellationRequested();
        return await decoder.GetSoftwareBitmapAsync(source.BitmapPixelFormat, source.BitmapAlphaMode);
    }

    /// <summary>
    /// 明るさとコントラストをインプレースで調整する（BGRA8形式専用）
    /// </summary>
    /// <param name="bitmap">調整対象のビットマップ（BGRA8形式）</param>
    /// <param name="brightness">明るさ（-127 - 128）</param>
    /// <param name="contrast">コントラスト（-99 - 100）</param>
    public static unsafe void AdjustBrightnessContrastInPlace(this SoftwareBitmap bitmap, int brightness, int contrast)
    {
        if (brightness == 0 && contrast == 0) return;

        using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.ReadWrite);
        using var reference = buffer.CreateReference();
        ((IMemoryBufferByteAccess)reference).GetBuffer(out var data, out var capacity);

        AdjustBrightnessContrast(new Span<byte>(data, (int)capacity), brightness, contrast);
    }

    /// <summary>
    /// 明るさとコントラストの調整処理（BGRA形式のSpanに適用、SIMDによる高速化）
    /// </summary>
    /// <param name="data">BGRA形式のピクセルデータ</param>
    /// <param name="brightness">明るさ（-127 - 128）</param>
    /// <param name="contrast">コントラスト（-99 - 100）</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AdjustBrightnessContrast(Span<byte> data, int brightness, int contrast)
    {
        // XMLコメントで規定されたレンジにクランプする
        brightness = Math.Clamp(brightness, -127, 128);
        contrast = Math.Clamp(contrast, -99, 100);

        // 固定小数点コントラスト係数 (×64スケール)
        // realContrast = (contrast + 100) / 100.0, range [0.01, 2.0]
        // contrastFixed = round(realContrast * 64), range [1, 128]
        var contrastFixed = (short)Math.Round((contrast + 100.0) / 100.0 * 64);
        var offset = (short)(128 + brightness);

        if (!Vector.IsHardwareAccelerated || data.Length < Vector<byte>.Count)
        {
            AdjustBrightnessContrastScalar(data, contrastFixed, offset);
            return;
        }

        // アルファチャンネルマスク: BGRA形式でA(index%4==3)は0x00, それ以外は0xFF
        // Vector<byte>.Count は常に4の倍数なのでマスクはBGRA境界に合わせられる
        Span<byte> maskData = stackalloc byte[Vector<byte>.Count];
        for (int i = 0; i < maskData.Length; i++)
            maskData[i] = (byte)(i % 4 == 3 ? 0 : 0xFF);
        var colorMask = Vector.LoadUnsafe(ref MemoryMarshal.GetReference(maskData));

        var contrastVec = new Vector<short>(contrastFixed);
        var offsetVec = new Vector<short>(offset);
        var sub128 = new Vector<short>(128);
        var zeroVec = Vector<short>.Zero;
        var maxVec = new Vector<short>(255);

        ref var current = ref MemoryMarshal.GetReference(data);
        ref var end = ref Unsafe.Add(ref current, data.Length);
        int vectorSize = Vector<byte>.Count;
        int dataLength = data.Length;
        int simdLength = dataLength - dataLength % vectorSize;
        ref var simdEnd = ref Unsafe.Add(ref current, simdLength);

        while (Unsafe.IsAddressLessThan(ref current, ref simdEnd))
        {
            var chunk = Vector.LoadUnsafe(ref current);

            // アルファバイトを保存 (chunk & ~colorMask)
            var alphaSaved = Vector.AndNot(chunk, colorMask);

            // バイトをshortに拡張してSIMD演算
            Vector.Widen(chunk, out var loU, out var hiU);
            var lo = loU.As<ushort, short>();
            var hi = hiU.As<ushort, short>();

            // (c - 128) * contrastFixed >> 6 + offset
            // 中間値の範囲: (±128) * 128 = ±16384、shortの範囲内
            lo -= sub128;
            lo *= contrastVec;
            lo = Vector.ShiftRightArithmetic(lo, 6);
            lo += offsetVec;
            lo = Vector.Min(Vector.Max(lo, zeroVec), maxVec);

            hi -= sub128;
            hi *= contrastVec;
            hi = Vector.ShiftRightArithmetic(hi, 6);
            hi += offsetVec;
            hi = Vector.Min(Vector.Max(hi, zeroVec), maxVec);

            // shortからバイトに縮小
            var result = Vector.Narrow(lo.As<short, ushort>(), hi.As<short, ushort>());

            // アルファを復元: (処理済みRGB) | (元のアルファ)
            result = (result & colorMask) | alphaSaved;

            result.StoreUnsafe(ref current);
            current = ref Unsafe.Add(ref current, Vector<byte>.Count);
        }

        // 残りのピクセルをスカラーで処理
        if (Unsafe.IsAddressLessThan(ref current, ref end))
        {
            var remaining = (int)Unsafe.ByteOffset(ref current, ref end);
            AdjustBrightnessContrastScalar(MemoryMarshal.CreateSpan(ref current, remaining), contrastFixed, offset);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AdjustBrightnessContrastScalar(Span<byte> data, short contrastFixed, short offset)
    {
        for (int i = 0; i + 3 < data.Length; i += 4)
        {
            data[i + 0] = ClampByte(((data[i + 0] - 128) * contrastFixed >> 6) + offset); // B
            data[i + 1] = ClampByte(((data[i + 1] - 128) * contrastFixed >> 6) + offset); // G
            data[i + 2] = ClampByte(((data[i + 2] - 128) * contrastFixed >> 6) + offset); // R
            // data[i + 3]: A は変更しない
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampByte(int val) => (byte)Math.Clamp(val, 0, 255);

    /// <summary>
    /// 画像を指定されたパスに保存する
    /// </summary>
    /// <remarks>
    /// エラー解析用のため、保存に失敗しても例外はスローしません。
    /// </remarks>
    /// <param name="source">保存する画像</param>
    /// <param name="path">保存先のパス</param>
    /// <returns>非同期操作</returns>
    public static async ValueTask TrySaveImage(this SoftwareBitmap source, string path)
    {
        try
        {
            // ディレクトリが存在しない場合は作成
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 拡張子からフォーマットを特定
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var encoderId = extension switch
            {
                ".jpg" or ".jpeg" => BitmapEncoder.JpegEncoderId,
                ".png" => BitmapEncoder.PngEncoderId,
                ".bmp" => BitmapEncoder.BmpEncoderId,
                ".tif" or ".tiff" => BitmapEncoder.TiffEncoderId,
                ".gif" => BitmapEncoder.GifEncoderId,
                ".heic" or ".heif" => BitmapEncoder.HeifEncoderId,
                _ => BitmapEncoder.JpegEncoderId // デフォルトはJPEG
            };

            // ファイルを作成
            using var fileStream = new FileStream(path, FileMode.Create);
            // IRandomAccessStreamに変換
            using var randomAccessStream = fileStream.AsRandomAccessStream();

            // エンコーダーを作成
            var encoder = await BitmapEncoder.CreateAsync(encoderId, randomAccessStream);

            // ビットマップをセット
            encoder.SetSoftwareBitmap(source);

            // フラッシュして保存
            await encoder.FlushAsync();
        }
        catch (Exception)
        {
            // エラー解析用のため、例外はスローしない
            // ここで何かログを残すことも可能ですが、今回は省略します
        }
    }
}

[Guid("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}
#endif
