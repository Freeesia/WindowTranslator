using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace WindowTranslator.Modules.Ocr;

/// <summary>
/// OCR の呼び出し元からフレームを受け取り、一定間隔で JSON Lines に追記する。
/// </summary>
public sealed class OcrTraceRecorder : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly object gate = new();
    private readonly ILogger<OcrTraceRecorder> logger;
    private readonly string directory;
    private readonly TimeSpan flushInterval;
    private readonly Channel<RecordingSession> sessions = Channel.CreateUnbounded<RecordingSession>(
        new UnboundedChannelOptions { SingleReader = true });
    private RecordingSession? current;
    private Task? writerTask;
    private bool disposed;

    public bool IsEnabled => Volatile.Read(ref this.current) is not null;

    public OcrTraceRecorder(ILogger<OcrTraceRecorder> logger)
        : this(logger, Path.Combine(PathUtility.UserDir, "ocr-traces"), TimeSpan.FromSeconds(1))
    {
    }

    internal OcrTraceRecorder(ILogger<OcrTraceRecorder> logger, string directory, TimeSpan flushInterval)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(flushInterval, TimeSpan.Zero);

        this.logger = logger;
        this.directory = directory;
        this.flushInterval = flushInterval;
    }

    public void Start()
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.current is not null)
            {
                return;
            }

            var session = new RecordingSession(
                Path.Combine(this.directory, $"ocr-{Guid.NewGuid():N}.jsonl"), CurrentTimestamp());
            if (!this.sessions.Writer.TryWrite(session))
            {
                throw new InvalidOperationException("OCR trace writer was closed while starting a recording.");
            }
            this.writerTask ??= Task.Run(this.WriteAsync);
            Volatile.Write(ref this.current, session);
            this.logger.LogInformation(
                "OCR trace recording started: {Path}. OCR text and geometry are saved; images are not saved. Review the file before sharing it.",
                session.Path);
        }
    }

    public void Stop()
    {
        lock (this.gate)
        {
            RecordingSession? session = this.current;
            Volatile.Write(ref this.current, null);
            session?.Frames.Writer.TryComplete();
        }
    }

    public void Record(IReadOnlyList<TextRect> observations, Size imageSize, TimeSpan timestamp)
    {
        if (!this.IsEnabled)
        {
            return;
        }

        try
        {
            lock (this.gate)
            {
                RecordingSession? session = this.current;
                if (session is null)
                {
                    return;
                }

                OcrTraceFrame frame = new(
                    (timestamp - session.OriginTime).Ticks,
                    imageSize.Width,
                    imageSize.Height,
                    observations.Select(OcrTraceRect.FromTextRect).ToArray());
                if (!session.Frames.Writer.TryWrite(frame))
                {
                    throw new InvalidOperationException("OCR trace writer was closed while recording.");
                }
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
        List<RecordingSession> active = [];
        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            while (this.sessions.Reader.TryRead(out RecordingSession? session))
            {
                active.Add(session);
            }
            for (int index = 0; index < active.Count;)
            {
                if (await this.FlushSessionAsync(active[index]).ConfigureAwait(false))
                {
                    active.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
            if (this.sessions.Reader.Completion.IsCompleted && active.Count == 0)
            {
                return;
            }
        }
    }

    private async Task<bool> FlushSessionAsync(RecordingSession session)
    {
        if (session.Pending.Count == 0)
        {
            while (session.Frames.Reader.TryRead(out OcrTraceFrame? frame))
            {
                session.Pending.Add(frame);
            }
        }

        if (session.Pending.Count > 0)
        {
            try
            {
                await this.AppendChunkAsync(session, session.Pending).ConfigureAwait(false);
                session.Pending.Clear();
                if (session.LoggedFailure)
                {
                    this.logger.LogInformation("OCR trace recording resumed: {Path}", session.Path);
                    session.LoggedFailure = false;
                }
            }
            catch (Exception error)
            {
                if (!session.LoggedFailure)
                {
                    this.logger.LogError(error, "OCR trace write failed; retrying: {Path}", session.Path);
                    session.LoggedFailure = true;
                }
                try
                {
                    await CloseWriterAsync(session).ConfigureAwait(false);
                }
                catch (Exception closeError)
                {
                    this.logger.LogError(closeError, "OCR trace writer could not be closed: {Path}", session.Path);
                }
            }
        }

        if (session.Frames.Reader.Completion.IsCompleted && session.Pending.Count == 0)
        {
            try
            {
                await CloseWriterAsync(session).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                this.logger.LogError(error, "OCR trace writer could not be closed: {Path}", session.Path);
            }
            return true;
        }
        return false;
    }

    private async Task AppendChunkAsync(RecordingSession session, IReadOnlyList<OcrTraceFrame> pending)
    {
        StringBuilder lines = new();
        if (session.CommittedLength == 0)
        {
            lines.AppendLine(JsonSerializer.Serialize(new OcrTraceHeader(OcrTraceHeader.CurrentVersion)));
        }
        foreach (OcrTraceFrame frame in pending)
        {
            lines.AppendLine(JsonSerializer.Serialize(frame, JsonOptions));
        }

        if (session.Writer is null)
        {
            Directory.CreateDirectory(this.directory);
            FileStream stream = new(session.Path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.Read, 4096, FileOptions.Asynchronous);
            try
            {
                if (stream.Length < session.CommittedLength)
                {
                    throw new IOException("OCR trace file was shortened while recording.");
                }
                if (stream.Length > session.CommittedLength)
                {
                    stream.SetLength(session.CommittedLength);
                }
                stream.Position = session.CommittedLength;
                session.Writer = new StreamWriter(stream, new UTF8Encoding(false));
            }
            catch
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        await session.Writer.WriteAsync(lines.ToString()).ConfigureAwait(false);
        await session.Writer.FlushAsync().ConfigureAwait(false);
        session.CommittedLength = session.Writer.BaseStream.Position;
    }

    private static async ValueTask CloseWriterAsync(RecordingSession session)
    {
        StreamWriter? writer = session.Writer;
        session.Writer = null;
        if (writer is null)
        {
            return;
        }

        Stream stream = writer.BaseStream;
        try
        {
            await writer.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? task;
        lock (this.gate)
        {
            this.disposed = true;
            RecordingSession? session = this.current;
            Volatile.Write(ref this.current, null);
            session?.Frames.Writer.TryComplete();
            this.sessions.Writer.TryComplete();
            task = this.writerTask;
        }

#pragma warning disable VSTHRD003 // 専用ライターの残りのフレームを待つ
        if (task is not null)
        {
            await task.ConfigureAwait(false);
        }
#pragma warning restore VSTHRD003
    }

    internal static TimeSpan CurrentTimestamp()
        => TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);

    private sealed class RecordingSession(string path, TimeSpan originTime)
    {
        public string Path { get; } = path;

        public TimeSpan OriginTime { get; } = originTime;

        public Channel<OcrTraceFrame> Frames { get; } = Channel.CreateUnbounded<OcrTraceFrame>(
            new UnboundedChannelOptions { SingleReader = true });

        public List<OcrTraceFrame> Pending { get; } = [];

        public StreamWriter? Writer { get; set; }

        public bool LoggedFailure { get; set; }

        public long CommittedLength { get; set; }
    }
}
