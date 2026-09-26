namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// JSON Lines の先頭行に保存する形式バージョン。
/// </summary>
internal sealed record OcrTraceHeader(int Version)
{
    public const int CurrentVersion = 2;
}

/// <summary>
/// 追跡処理に渡す前の統合済みOCR結果。ヘッダー以降は1行を1フレームとして保存する。
/// </summary>
internal sealed record OcrTraceFrame(
    int ImageWidth,
    int ImageHeight,
    IReadOnlyList<OcrTraceRect> Observations);

/// <summary>
/// 再生に必要な情報だけを保存し、画像や翻訳結果は含めない。
/// </summary>
internal sealed record OcrTraceRect(
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
}
