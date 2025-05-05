using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WindowTranslator.Plugin.OneOcrPlugin;

partial class NativeMethods
{
    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long CreateOcrInitOptions(out long ctx);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLineCount(long instance, out long count);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLine(long instance, long index, out long line);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLineContent(long line, out IntPtr content);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLineBoundingBox(long line, out BoundingBox boundingBox);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLineWordCount(long instance, out long count);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrWord(long instance, long index, out long line);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrWordContent(long line, out IntPtr content);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrWordBoundingBox(long line, out BoundingBox boundingBox);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long OcrProcessOptionsSetMaxRecognitionLineCount(long opt, long count);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long RunOcrPipeline(long pipeline, ref Img img, long opt, out long instance);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long CreateOcrProcessOptions(out long opt);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long OcrInitOptionsSetUseModelDelayLoad(long ctx, byte flag);

    [LibraryImport("oneocr.dll", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long CreateOcrPipeline(string modelPath, string key, long ctx, out long pipeline);
}

[StructLayout(LayoutKind.Sequential)]
readonly record struct Img(int T, int Col, int Row, int Unk, long Step, IntPtr Data);

[StructLayout(LayoutKind.Sequential)]
readonly struct BoundingBox
{
    public readonly float x1;
    public readonly float y1;
    public readonly float x2;
    public readonly float y2;
    public readonly float x3;
    public readonly float y3;
    public readonly float x4;
    public readonly float y4;
}
