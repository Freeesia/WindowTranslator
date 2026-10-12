using System.Drawing;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.TraceViewer;

internal sealed class TraceReplay(IReadOnlyList<OcrTraceFrame> frames)
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public IReadOnlyList<ReplayFrame> Results { get; private set; } = [];

    public static TraceReplay Load(string path, TargetSettings settings)
    {
        using StreamReader reader = new(path);
        OcrTraceHeader header = ReadLine<OcrTraceHeader>(reader.ReadLine(), 1);
        if (header.Version != OcrTraceHeader.CurrentVersion)
        {
            throw new InvalidDataException($"未対応のトレース形式バージョンです: {header.Version}");
        }

        List<OcrTraceFrame> frames = [];
        long previousTicks = 0;
        int lineNumber = 1;
        while (reader.ReadLine() is string line)
        {
            lineNumber++;
            OcrTraceFrame frame = ReadLine<OcrTraceFrame>(line, lineNumber);
            if (frame.RelativeTimeTicks < previousTicks || frame.ImageWidth <= 0 || frame.ImageHeight <= 0
                || frame.Observations is null || frame.Observations.Any(rect => rect is null
                    || rect.SourceText is null
                    || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
                    || !double.IsFinite(rect.Width) || rect.Width <= 0
                    || !double.IsFinite(rect.Height) || rect.Height <= 0
                    || !double.IsFinite(rect.FontSize) || rect.FontSize <= 0
                    || !double.IsFinite(rect.Angle)))
            {
                throw new InvalidDataException($"{lineNumber} 行目のフレームに不正な時刻、画像サイズ、または OCR 矩形があります。");
            }
            frames.Add(frame);
            previousTicks = frame.RelativeTimeTicks;
        }
        if (frames.Count == 0)
        {
            throw new InvalidDataException("トレースにフレームがありません。");
        }

        TraceReplay replay = new(frames);
        replay.Recalculate(settings);
        return replay;
    }

    private static T ReadLine<T>(string? line, int lineNumber) where T : class
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidDataException($"{lineNumber} 行目に JSON データがありません。");
        }
        try
        {
            return JsonSerializer.Deserialize<T>(line, jsonOptions)
                ?? throw new InvalidDataException($"{lineNumber} 行目が null です。");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"{lineNumber} 行目の JSON が不正です: {error.Message}", error);
        }
    }

    public void Recalculate(TargetSettings settings)
    {
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance, settings);
        List<ReplayFrame> results = new(frames.Count);
        foreach (OcrTraceFrame frame in frames)
        {
            TextRect[] raw = frame.Observations.Select(rect => new TextRect(
                rect.SourceText, rect.X, rect.Y, rect.Width, rect.Height, rect.FontSize, rect.MultiLine)
            {
                Angle = rect.Angle,
            }).ToArray();

            // 再生操作の時刻を使わず、毎回記録順・記録時刻で先頭から計算する。
            IReadOnlyList<TextRect> tracked = tracker.Update(raw,
                new Size(frame.ImageWidth, frame.ImageHeight), TimeSpan.FromTicks(frame.RelativeTimeTicks));
            results.Add(new(frame, raw, tracked));
        }
        Results = results;
    }
}

internal sealed record ReplayFrame(OcrTraceFrame Source, IReadOnlyList<TextRect> Raw, IReadOnlyList<TextRect> Tracked);
