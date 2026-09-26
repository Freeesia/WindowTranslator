using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WindowTranslator.Modules.Ocr;

namespace WindowTranslator.Tests;

public class OcrTraceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    [Fact]
    public async Task RecorderAppendsDuringRecordingAndPreservesOrderAndEachImageSize()
    {
        string directory = NewTemporaryDirectory();
        try
        {
            await using var recorder = new OcrTraceRecorder(
                NullLogger<OcrTraceRecorder>.Instance, directory, TimeSpan.FromMilliseconds(20));
            Assert.False(recorder.IsEnabled);
            recorder.Start();
            recorder.Record([new TextRect("秘密", 11, 22, 33, 44, 15, true) { Angle = 12.5 }],
                new Size(800, 600));

            string path = await WaitForTraceAsync(directory);
            await using var sharedReader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            OcrTraceFrame[] firstRead = await ReadFramesAsync(sharedReader);
            Assert.Single(firstRead);

            recorder.Record([], new Size(1024, 768));
            recorder.Stop();
            await recorder.DisposeAsync();

            sharedReader.Position = 0;
            OcrTraceFrame[] frames = await ReadFramesAsync(sharedReader);
            Assert.Equal(2, frames.Length);
            Assert.Equal((800, 600), (frames[0].ImageWidth, frames[0].ImageHeight));
            Assert.Equal((1024, 768), (frames[1].ImageWidth, frames[1].ImageHeight));
            Assert.Equal(new OcrTraceRect("秘密", 11, 22, 33, 44, 15, 12.5, true),
                Assert.Single(frames[0].Observations));
            Assert.Empty(frames[1].Observations);
            Assert.Single(Directory.GetFiles(directory, "*.jsonl"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SavedFramesCanBeFedToTrackerInFileOrder()
    {
        OcrTraceFrame[] source =
        [
            new(800, 600, [new("Menu", 100, 100, 80, 20, 16, 0, false)]),
            new(1024, 768, [new("Menu", 102, 100, 80, 20, 16, 0, false)]),
        ];
        string json = JsonSerializer.Serialize(new OcrTraceHeader(OcrTraceHeader.CurrentVersion)) + "\n"
            + string.Join("\n", source.Select(frame => JsonSerializer.Serialize(frame))) + "\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        OcrTraceFrame[] frames = await ReadFramesAsync(stream);
        OcrTextTracker tracker = new(NullLogger<OcrTextTracker>.Instance);
        List<IReadOnlyList<TextRect>> outputs = [];

        for (int index = 0; index < frames.Length; index++)
        {
            OcrTraceFrame frame = frames[index];
            TextRect[] observations = frame.Observations.Select(RestoreRect).ToArray();
            outputs.Add(tracker.Update(observations, new Size(frame.ImageWidth, frame.ImageHeight),
                TimeSpan.FromMilliseconds(index * 500)));
        }

        Assert.Equal(2, outputs.Count);
        Assert.Equal("Menu", Assert.Single(outputs[0]).SourceText);
        Assert.Equal((1024, 768), (frames[1].ImageWidth, frames[1].ImageHeight));
    }

    [Fact]
    public async Task DisabledRecorderDoesNotCreateFiles()
    {
        string directory = NewTemporaryDirectory();
        await using (var recorder = new OcrTraceRecorder(
            NullLogger<OcrTraceRecorder>.Instance, directory, TimeSpan.FromMilliseconds(20)))
        {
            Assert.False(recorder.IsEnabled);
            recorder.Record([new TextRect("Menu", 0, 0, 10, 10, 8, false)], new Size(100, 100));
        }
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task WriteFailureIsLoggedAndPendingFramesAreRetried()
    {
        string parent = NewTemporaryDirectory();
        Directory.CreateDirectory(parent);
        string directory = Path.Combine(parent, "blocked");
        await File.WriteAllTextAsync(directory, "existing file");
        TestLogger logger = new();
        var recorder = new OcrTraceRecorder(logger, directory, TimeSpan.FromMilliseconds(20));

        try
        {
            TextRect observation = new("Menu", 0, 0, 10, 10, 8, false);
            recorder.Start();
            recorder.Record([observation], new Size(100, 100));
            await logger.FirstError.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(recorder.IsEnabled);
            Assert.Equal("Menu", Assert.Single(new OcrTextTracker(
                NullLogger<OcrTextTracker>.Instance).Update([observation], new Size(100, 100))).SourceText);

            File.Delete(directory);
            recorder.Record([], new Size(120, 100));
            recorder.Stop();
            await recorder.DisposeAsync();

            string path = Assert.Single(Directory.GetFiles(directory, "*.jsonl"));
            await using var stream = File.OpenRead(path);
            OcrTraceFrame[] frames = await ReadFramesAsync(stream);
            Assert.Equal(2, frames.Length);
            Assert.Equal("Menu", Assert.Single(frames[0].Observations).SourceText);
            Assert.Empty(frames[1].Observations);
        }
        finally
        {
            if (File.Exists(directory)) File.Delete(directory);
            await recorder.DisposeAsync();
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task ReaderAcceptsExtraFieldsAndRejectsUnknownVersion()
    {
        string compatible = $$"""
            {"Version":{{OcrTraceHeader.CurrentVersion}},"FutureHeaderField":"ignored"}
            {"ImageWidth":100,"ImageHeight":100,"Observations":[],"FutureFrameField":"ignored"}
            """;
        await using var compatibleStream = new MemoryStream(Encoding.UTF8.GetBytes(compatible));
        Assert.Single(await ReadFramesAsync(compatibleStream));

        string incompatible = compatible.Replace($"\"Version\":{OcrTraceHeader.CurrentVersion}",
            "\"Version\":1", StringComparison.Ordinal);
        await using var incompatibleStream = new MemoryStream(Encoding.UTF8.GetBytes(incompatible));
        await Assert.ThrowsAsync<NotSupportedException>(() => ReadFramesAsync(incompatibleStream));

        const string oldInlineVersion = """
            {"Version":1,"FrameNumber":1,"RelativeTimeTicks":0,"ImageWidth":100,"ImageHeight":100,"Observations":[]}
            """;
        await using var oldStream = new MemoryStream(Encoding.UTF8.GetBytes(oldInlineVersion));
        await Assert.ThrowsAsync<NotSupportedException>(() => ReadFramesAsync(oldStream));
    }

    private static async Task<OcrTraceFrame[]> ReadFramesAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        string headerLine = await reader.ReadLineAsync()
            ?? throw new InvalidDataException("OCR trace header is missing.");
        OcrTraceHeader header = JsonSerializer.Deserialize<OcrTraceHeader>(headerLine)
            ?? throw new InvalidDataException("OCR trace header is invalid.");
        if (header.Version != OcrTraceHeader.CurrentVersion)
        {
            throw new NotSupportedException($"Unsupported OCR trace version: {header.Version}.");
        }

        List<OcrTraceFrame> frames = [];
        while (await reader.ReadLineAsync() is { } line)
        {
            frames.Add(JsonSerializer.Deserialize<OcrTraceFrame>(line, JsonOptions)
                ?? throw new InvalidDataException("OCR trace frame is invalid."));
        }
        return frames.ToArray();
    }

    private static TextRect RestoreRect(OcrTraceRect rect)
        => new(rect.SourceText, rect.X, rect.Y, rect.Width, rect.Height, rect.FontSize, rect.MultiLine)
        {
            Angle = rect.Angle,
        };

    private static async Task<string> WaitForTraceAsync(string directory)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (Directory.Exists(directory))
            {
                string[] files = Directory.GetFiles(directory, "*.jsonl");
                if (files.Length == 1)
                {
                    await using var stream = new FileStream(files[0], FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    string content = await reader.ReadToEndAsync();
                    if (content.Count(character => character == '\n') >= 2)
                    {
                        return files[0];
                    }
                }
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("OCR trace was not appended while recording.");
    }

    private static string NewTemporaryDirectory()
        => Path.Combine(Path.GetTempPath(), nameof(OcrTraceTests), Guid.NewGuid().ToString("N"));

    private sealed class TestLogger : ILogger<OcrTraceRecorder>
    {
        public TaskCompletionSource<Exception?> FirstError { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                this.FirstError.TrySetResult(exception);
            }
        }
    }
}
