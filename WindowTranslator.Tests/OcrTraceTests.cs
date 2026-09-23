using System.Drawing;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

public class OcrTraceTests
{
    [Fact]
    public async Task RecorderSavesMergedObservationsInFrameOrder()
    {
        string directory = NewTemporaryDirectory();
        try
        {
            await using (var recorder = new OcrTraceRecorder(
                new() { Enabled = true, Directory = directory },
                NullLogger<OcrTraceRecorder>.Instance))
            {
                recorder.Record([new TextRect("秘密", 11, 22, 33, 44, 15, true) { Angle = 12.5 }],
                    new Size(800, 600));
                await Task.Delay(10);
                recorder.Record([], new Size(1024, 768));
            }

            string path = Assert.Single(Directory.GetFiles(directory, "*.jsonl"));
            await using var stream = File.OpenRead(path);
            List<OcrTraceFrame> frames = [];
            await foreach (OcrTraceFrame frame in OcrTraceReplay.ReadFramesAsync(stream))
            {
                frames.Add(frame);
            }

            Assert.Equal([1L, 2L], frames.Select(frame => frame.FrameNumber));
            Assert.True(frames[0].RelativeTimeTicks >= 0);
            Assert.True(frames[1].RelativeTimeTicks > frames[0].RelativeTimeTicks);
            Assert.Equal((800, 600), (frames[0].ImageWidth, frames[0].ImageHeight));
            Assert.Equal((1024, 768), (frames[1].ImageWidth, frames[1].ImageHeight));
            OcrTraceRect rect = Assert.Single(frames[0].Observations);
            Assert.Equal(new OcrTraceRect("秘密", 11, 22, 33, 44, 15, 12.5, true), rect);
            Assert.Empty(frames[1].Observations);
            Assert.Single(Directory.GetFiles(directory));

            stream.Position = 0;
            OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
            List<OcrTraceReplayResult> replayed = [];
            await foreach (OcrTraceReplayResult result in OcrTraceReplay.ReplayAsync(stream, tracker))
            {
                replayed.Add(result);
            }
            Assert.Equal("秘密", Assert.Single(replayed[0].Stabilized).SourceText);
            Assert.Empty(replayed[1].Stabilized);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ReplayUsesRecordedSizeOrderAndTime()
    {
        OcrTraceFrame[] frames =
        [
            new(1, 1, 0, 800, 600, [new("Menu", 100, 100, 80, 20, 16, 0, false)]),
            new(1, 2, TimeSpan.FromMilliseconds(500).Ticks, 1024, 768,
                [new("Menu", 102, 100, 80, 20, 16, 0, false)]),
        ];
        string json = string.Join("\n", frames.Select(frame => JsonSerializer.Serialize(frame))) + "\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        List<OcrTraceReplayResult> results = [];
        await foreach (OcrTraceReplayResult result in OcrTraceReplay.ReplayAsync(stream, tracker))
        {
            results.Add(result);
        }

        Assert.Equal([1L, 2L], results.Select(result => result.Frame.FrameNumber));
        Assert.Equal(frames[1].RelativeTimeTicks, results[1].Frame.RelativeTimeTicks);
        Assert.All(results, result => Assert.Equal("Menu", Assert.Single(result.Stabilized).SourceText));
        Assert.All(results, result => Assert.True(result.ProcessingTime >= TimeSpan.Zero));
    }

    [Fact]
    public async Task DisabledRecorderDoesNotCreateFiles()
    {
        string directory = NewTemporaryDirectory();
        await using (var recorder = new OcrTraceRecorder(
            new() { Directory = directory }, NullLogger<OcrTraceRecorder>.Instance))
        {
            Assert.False(recorder.IsEnabled);
            recorder.Record([new TextRect("Menu", 0, 0, 10, 10, 8, false)], new Size(100, 100));
        }
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task WriteFailureDoesNotStopTracking()
    {
        string path = Path.Combine(NewTemporaryDirectory(), "occupied");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "existing file");
        try
        {
            await using (var recorder = new OcrTraceRecorder(
                new() { Enabled = true, Directory = path },
                NullLogger<OcrTraceRecorder>.Instance))
            {
                TextRect observation = new("Menu", 0, 0, 10, 10, 8, false);
                recorder.Record([observation], new Size(100, 100));
                OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
                Assert.Equal("Menu", Assert.Single(tracker.Update([observation], new Size(100, 100))).SourceText);
            }
            Assert.Equal("existing file", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task ReaderAcceptsExtraFieldsAndRejectsUnknownVersion()
    {
        string compatible = """
            {"Version":1,"FrameNumber":1,"RelativeTimeTicks":0,"ImageWidth":100,"ImageHeight":100,"Observations":[],"FutureField":"ignored"}
            """;
        await using var compatibleStream = new MemoryStream(Encoding.UTF8.GetBytes(compatible));
        await foreach (OcrTraceFrame frame in OcrTraceReplay.ReadFramesAsync(compatibleStream))
        {
            Assert.Equal(OcrTraceFrame.CurrentVersion, frame.Version);
        }

        string incompatible = compatible.Replace("\"Version\":1", "\"Version\":2", StringComparison.Ordinal);
        await using var incompatibleStream = new MemoryStream(Encoding.UTF8.GetBytes(incompatible));
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (OcrTraceFrame _ in OcrTraceReplay.ReadFramesAsync(incompatibleStream))
            {
            }
        });
    }

    private static string NewTemporaryDirectory()
        => Path.Combine(Path.GetTempPath(), nameof(OcrTraceTests), Guid.NewGuid().ToString("N"));
}
