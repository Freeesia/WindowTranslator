using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace StudioFreesia.ColorThief;

static class Simd
{
    public static void Rshift(Span<byte> source, int shift)
    {
        if (!Vector128.IsHardwareAccelerated || source.Length < Vector128<byte>.Count)
        {
            foreach (ref var item in source)
            {
                item >>= shift;
            }
        }
        else if (!Vector256.IsHardwareAccelerated || source.Length < Vector256<byte>.Count)
        {
            ref var current = ref MemoryMarshal.GetReference(source);
            ref var end = ref Unsafe.Add(ref current, source.Length);
            ref var to = ref Unsafe.Add(ref current, source.Length - Vector128<byte>.Count);

            // SIMDを使用して処理
            while (Unsafe.IsAddressLessThan(ref current, ref to))
            {
                var chunk = Vector128.LoadUnsafe(ref current);
                chunk = Vector128.ShiftRightLogical(chunk, shift);
                chunk.StoreUnsafe(ref current);
                current = ref Unsafe.Add(ref current, Vector128<byte>.Count);
            }

            // SIMDで処理できなかった余り部分を処理
            if (Unsafe.IsAddressLessThan(ref current, ref end))
            {
                var remainingBytes = source.Length % Vector128<byte>.Count;
                ref var last = ref Unsafe.Add(ref current, -remainingBytes);
                var lastChunk = Vector128.LoadUnsafe(ref last, (nuint)remainingBytes);
                lastChunk = Vector128.ShiftRightLogical(lastChunk, shift);
                lastChunk.StoreUnsafe(ref last, (nuint)remainingBytes);
            }
        }
        else if (!Vector512.IsHardwareAccelerated || source.Length < Vector512<byte>.Count)
        {
            ref var current = ref MemoryMarshal.GetReference(source);
            ref var end = ref Unsafe.Add(ref current, source.Length);
            ref var to = ref Unsafe.Add(ref current, source.Length - Vector256<byte>.Count);

            // SIMDを使用して処理
            while (Unsafe.IsAddressLessThan(ref current, ref to))
            {
                var chunk = Vector256.LoadUnsafe(ref current);
                chunk = Vector256.ShiftRightLogical(chunk, shift);
                chunk.StoreUnsafe(ref current);
                current = ref Unsafe.Add(ref current, Vector256<byte>.Count);
            }

            // SIMDで処理できなかった余り部分を処理
            if (Unsafe.IsAddressLessThan(ref current, ref end))
            {
                var remainingBytes = source.Length % Vector256<byte>.Count;
                ref var last = ref Unsafe.Add(ref current, -remainingBytes);
                var lastChunk = Vector256.LoadUnsafe(ref last, (nuint)remainingBytes);
                lastChunk = Vector256.ShiftRightLogical(lastChunk, shift);
                lastChunk.StoreUnsafe(ref last, (nuint)remainingBytes);
            }
        }
        else
        {
            ref var current = ref MemoryMarshal.GetReference(source);
            ref var end = ref Unsafe.Add(ref current, source.Length);
            ref var to = ref Unsafe.Add(ref current, source.Length - Vector512<byte>.Count);

            // SIMDを使用して処理
            while (Unsafe.IsAddressLessThan(ref current, ref to))
            {
                var chunk = Vector512.LoadUnsafe(ref current);
                chunk = Vector512.ShiftRightLogical(chunk, shift);
                chunk.StoreUnsafe(ref current);
                current = ref Unsafe.Add(ref current, Vector512<byte>.Count);
            }

            // SIMDで処理できなかった余り部分を処理
            if (Unsafe.IsAddressLessThan(ref current, ref end))
            {
                var remainingBytes = source.Length % Vector512<byte>.Count;
                ref var last = ref Unsafe.Add(ref current, -remainingBytes);
                var lastChunk = Vector512.LoadUnsafe(ref last, (nuint)remainingBytes);
                lastChunk = Vector512.ShiftRightLogical(lastChunk, shift);
                lastChunk.StoreUnsafe(ref last, (nuint)remainingBytes);
            }
        }
    }

    public static (byte min, byte mas) MinMax(ReadOnlySpan<byte> source)
    {
        if (!Vector128.IsHardwareAccelerated || source.Length < Vector128<byte>.Count)
        {
            var min = source[0];
            var max = min;
            for (int i = 1; i < source.Length; i++)
            {
                min = Math.Min(min, source[i]);
                max = Math.Max(max, source[i]);
            }
            return (min, max);
        }
        else if (!Vector256.IsHardwareAccelerated || source.Length < Vector256<byte>.Count)
        {
            ref var current = ref MemoryMarshal.GetReference(source);
            ref var to = ref Unsafe.Add(ref current, source.Length - Vector128<byte>.Count);

            var vectorMin = Vector128.LoadUnsafe(ref current);
            var vectorMax = vectorMin;
            current = ref Unsafe.Add(ref current, Vector128<byte>.Count);
            while (Unsafe.IsAddressLessThan(ref current, ref to))
            {
                vectorMin = Vector128.Min(vectorMin, Vector128.LoadUnsafe(ref current));
                vectorMax = Vector128.Max(vectorMax, Vector128.LoadUnsafe(ref current));
                current = ref Unsafe.Add(ref current, Vector128<byte>.Count);
            }
            vectorMin = Vector128.Min(vectorMin, Vector128.LoadUnsafe(ref to));
            vectorMax = Vector128.Max(vectorMax, Vector128.LoadUnsafe(ref to));

            var min = vectorMin[0];
            var max = vectorMax[0];
            for (int i = 1; i < Vector128<byte>.Count; i++)
            {
                min = Math.Min(min, vectorMin[i]);
                max = Math.Max(max, vectorMax[i]);
            }
            return (min, max);
        }
        else
        {
            ref var current = ref MemoryMarshal.GetReference(source);
            ref var to = ref Unsafe.Add(ref current, source.Length - Vector256<byte>.Count);

            var vectorMin = Vector256.LoadUnsafe(ref current);
            var vectorMax = vectorMin;
            current = ref Unsafe.Add(ref current, Vector256<byte>.Count);
            while (Unsafe.IsAddressLessThan(ref current, ref to))
            {
                vectorMin = Vector256.Min(vectorMin, Vector256.LoadUnsafe(ref current));
                vectorMax = Vector256.Max(vectorMax, Vector256.LoadUnsafe(ref current));
                current = ref Unsafe.Add(ref current, Vector256<byte>.Count);
            }
            vectorMin = Vector256.Min(vectorMin, Vector256.LoadUnsafe(ref to));
            vectorMax = Vector256.Max(vectorMax, Vector256.LoadUnsafe(ref to));

            var min = vectorMin[0];
            var max = vectorMax[0];
            for (int i = 1; i < Vector256<byte>.Count; i++)
            {
                min = Math.Min(min, vectorMin[i]);
                max = Math.Max(max, vectorMax[i]);
            }
            return (min, max);
        }
        // TODO: 512bitの場合の処理を追加
    }
}
