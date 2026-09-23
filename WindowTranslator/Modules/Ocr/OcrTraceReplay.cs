using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// JSON Linesトレースを元の順序・時刻・画像サイズで追跡処理に入力する。
/// </summary>
public static class OcrTraceReplay
{
    public static async IAsyncEnumerable<OcrTraceFrame> ReadFramesAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        long previousFrame = 0;
        long previousTime = -1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            OcrTraceFrame frame = JsonSerializer.Deserialize<OcrTraceFrame>(line)
                ?? throw new JsonException("OCR trace frame is null.");
            if (frame.Version != OcrTraceFrame.CurrentVersion)
            {
                throw new NotSupportedException($"Unsupported OCR trace version: {frame.Version}.");
            }
            if (frame.FrameNumber <= previousFrame || frame.RelativeTimeTicks < previousTime
                || frame.RelativeTimeTicks < 0 || frame.ImageWidth <= 0 || frame.ImageHeight <= 0
                || frame.Observations is null || frame.Observations.Any(rect => rect is null || rect.SourceText is null))
            {
                throw new JsonException("OCR trace frame has invalid order, time, size, or observations.");
            }

            previousFrame = frame.FrameNumber;
            previousTime = frame.RelativeTimeTicks;
            yield return frame;
        }
    }

    public static async IAsyncEnumerable<OcrTraceReplayResult> ReplayAsync(
        Stream stream,
        OcrTextTracker tracker,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        tracker.Reset();
        Size previousSize = Size.Empty;
        await foreach (OcrTraceFrame frame in ReadFramesAsync(stream, cancellationToken))
        {
            Size imageSize = new(frame.ImageWidth, frame.ImageHeight);
            if (previousSize != Size.Empty && previousSize != imageSize)
            {
                tracker.Reset();
            }
            previousSize = imageSize;
            TextRect[] observations = frame.Observations.Select(rect => rect.ToTextRect()).ToArray();
            long start = Stopwatch.GetTimestamp();
            IReadOnlyList<TextRect> stabilized = tracker.Update(
                observations,
                imageSize,
                TimeSpan.FromTicks(frame.RelativeTimeTicks));
            TimeSpan processingTime = Stopwatch.GetElapsedTime(start);
            yield return new(frame, stabilized, processingTime);
        }
    }
}

public sealed record OcrTraceReplayResult(
    OcrTraceFrame Frame,
    IReadOnlyList<TextRect> Stabilized,
    TimeSpan ProcessingTime);
