using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace WindowTranslator.Modules.Ocr;

public sealed class OcrTraceRecorder : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly ILogger<OcrTraceRecorder> logger;
    private readonly string directory;
    private readonly string path;
    private readonly TimeSpan flushInterval;
    private readonly Channel<OcrTraceFrame> frames = Channel.CreateUnbounded<OcrTraceFrame>(
        new UnboundedChannelOptions { SingleReader = true });
    private Task? writerTask;
    private long originTimestamp;
    private long committedLength;
    private bool enabled;
    private bool disposed;

    public bool IsEnabled => Volatile.Read(ref this.enabled);

    public OcrTraceRecorder(ILogger<OcrTraceRecorder> logger)
        : this(logger, Path.Combine(PathUtility.UserDir, "ocr-traces"), TimeSpan.FromSeconds(1))
    {
    }

    internal OcrTraceRecorder(ILogger<OcrTraceRecorder> logger, string directory, TimeSpan flushInterval)
    {
        this.logger = logger;
        this.directory = directory;
        this.path = Path.Combine(directory, $"ocr-{Guid.NewGuid():N}.jsonl");
        this.flushInterval = flushInterval;
    }

    public void Start()
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.enabled)
            {
                return;
            }
            if (this.writerTask is null)
            {
                this.originTimestamp = Stopwatch.GetTimestamp();
                this.writerTask = this.WriteAsync();
            }
            Volatile.Write(ref this.enabled, true);
            this.logger.LogInformation("OCR trace recording started: {Path}", this.path);
        }
    }

    public void Stop()
    {
        lock (this.gate)
        {
            Volatile.Write(ref this.enabled, false);
        }
    }

    public void Record(IReadOnlyList<TextRect> observations, Size imageSize)
    {
        if (!this.IsEnabled)
        {
            return;
        }

        try
        {
            OcrTraceRect[] rects = observations.Select(OcrTraceRect.FromTextRect).ToArray();
            if (this.IsEnabled)
            {
                this.frames.Writer.TryWrite(new(
                    Stopwatch.GetElapsedTime(this.originTimestamp).Ticks,
                    imageSize.Width,
                    imageSize.Height,
                    rects));
            }
        }
        catch (Exception error)
        {
            this.logger.LogError(error, "OCR trace frame could not be queued.");
        }
    }

    private async Task WriteAsync()
    {
        using var timer = new PeriodicTimer(this.flushInterval);
        List<OcrTraceFrame> pending = [];
        StreamWriter? writer = null;
        try
        {
            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                while (this.frames.Reader.TryRead(out OcrTraceFrame? frame))
                {
                    pending.Add(frame);
                }
                if (pending.Count == 0)
                {
                    if (this.frames.Reader.Completion.IsCompleted)
                    {
                        return;
                    }
                    continue;
                }

                try
                {
                    if (writer is null)
                    {
                        Directory.CreateDirectory(this.directory);
                        FileStream stream = new(this.path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                            FileShare.Read, 4096, FileOptions.Asynchronous);
                        try
                        {
                            stream.SetLength(this.committedLength);
                            stream.Position = this.committedLength;
                            writer = new StreamWriter(stream, new UTF8Encoding(false));
                        }
                        catch
                        {
                            await stream.DisposeAsync().ConfigureAwait(false);
                            throw;
                        }
                    }

                    StringBuilder chunk = new();
                    if (this.committedLength == 0)
                    {
                        chunk.AppendLine(JsonSerializer.Serialize(new OcrTraceHeader(OcrTraceHeader.CurrentVersion)));
                    }
                    foreach (OcrTraceFrame frame in pending)
                    {
                        chunk.AppendLine(JsonSerializer.Serialize(frame));
                    }
                    await writer.WriteAsync(chunk.ToString()).ConfigureAwait(false);
                    await writer.FlushAsync().ConfigureAwait(false);
                    this.committedLength = writer.BaseStream.Position;
                    pending.Clear();
                }
                catch (Exception error)
                {
                    this.logger.LogError(error, "OCR trace write failed; retrying: {Path}", this.path);
                    if (writer is not null)
                    {
                        await this.CloseWriterAsync(writer).ConfigureAwait(false);
                        writer = null;
                    }
                }
            }
        }
        finally
        {
            if (writer is not null)
            {
                await this.CloseWriterAsync(writer).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask CloseWriterAsync(StreamWriter writer)
    {
        try
        {
            await writer.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            this.logger.LogError(error, "OCR trace writer could not be closed: {Path}", this.path);
            try
            {
                await writer.BaseStream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception closeError)
            {
                this.logger.LogError(closeError, "OCR trace stream could not be closed: {Path}", this.path);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? task;
        lock (this.gate)
        {
            this.disposed = true;
            Volatile.Write(ref this.enabled, false);
            this.frames.Writer.TryComplete();
            task = this.writerTask;
        }

#pragma warning disable VSTHRD003 // 専用ライターの残りのフレームを待つ
        if (task is not null)
        {
            await task.ConfigureAwait(false);
        }
#pragma warning restore VSTHRD003
    }
}
