using System.Drawing;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// 追跡処理に渡す前の統合済みOCR結果を表す。1行を1フレームとしてJSON Linesに保存する。
/// </summary>
public sealed record OcrTraceFrame(
    int Version,
    long FrameNumber,
    long RelativeTimeTicks,
    int ImageWidth,
    int ImageHeight,
    IReadOnlyList<OcrTraceRect> Observations)
{
    public const int CurrentVersion = 1;
}

/// <summary>
/// 再生に必要な情報だけを保存し、画像や翻訳結果は含めない。
/// </summary>
public sealed record OcrTraceRect(
    string SourceText,
    double X,
    double Y,
    double Width,
    double Height,
    double FontSize,
    double Angle,
    bool MultiLine)
{
    public static OcrTraceRect FromTextRect(TextRect rect)
        => new(rect.SourceText, rect.X, rect.Y, rect.Width, rect.Height,
            rect.FontSize, rect.Angle, rect.MultiLine);

    public TextRect ToTextRect()
        => new(SourceText, X, Y, Width, Height, FontSize, MultiLine)
        {
            Angle = Angle,
        };
}
