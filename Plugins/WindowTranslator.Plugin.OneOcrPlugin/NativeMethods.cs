﻿using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WindowTranslator.Plugin.OneOcrPlugin;

static partial class NativeMethods
{
    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long CreateOcrInitOptions(out long ctx);

    [LibraryImport("oneocr.dll", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long CreateOcrPipeline(string modelPath, string key, long ctx, out long pipeline);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long CreateOcrProcessOptions(out long opt);

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long GetImageAngle();

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLine(long instance, long index, out long line);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLineBoundingBox(long line, out IntPtr boundingBox);

    [LibraryImport("oneocr.dll", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLineContent(long line, out string content);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLineCount(long instance, out long count);

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long GetOcrLineStyle();

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrLineWordCount(long instance, out long count);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrWord(long instance, long index, out long line);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrWordBoundingBox(long line, out IntPtr boundingBox);

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long GetOcrWordConfidence();

    [LibraryImport("oneocr.dll", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long GetOcrWordContent(long line, out string content);

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long OcrInitOptionsSetUseModelDelayLoad(long ctx, byte flag);

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long OcrProcessOptionsGetMaxRecognitionLineCount();

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long OcrProcessOptionsGetResizeResolution();

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long OcrProcessOptionsSetMaxRecognitionLineCount(long opt, long count);

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long OcrProcessOptionsSetResizeResolution();

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long ReleaseOcrInitOptions();

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long ReleaseOcrPipeline();

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long ReleaseOcrProcessOptions();

    // [LibraryImport("oneocr.dll")]
    // [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    // public static partial long ReleaseOcrResult();

    [LibraryImport("oneocr.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial long RunOcrPipeline(long pipeline, ref Img img, long opt, out long instance);
}

[StructLayout(LayoutKind.Sequential)]
readonly record struct Img(int T, int Col, int Row, int Unk, long Step, IntPtr Data);

[StructLayout(LayoutKind.Sequential)]
struct BoundingBox
{
    public float x1;
    public float y1;
    public float x2;
    public float y2;
    public float x3;
    public float y3;
    public float x4;
    public float y4;
}
