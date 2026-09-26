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
    private readonly List<Task> writerTasks = [];
    private RecordingSession? current;
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

            var session = new RecordingSession(Path.Combine(this.directory, $"ocr-{Guid.NewGuid():N}.jsonl"));
            Volatile.Write(ref this.current, session);
            this.writerTasks.Add(Task.Run(() => WriteAsync(session)));
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

    public void Record(IReadOnlyList<TextRect> observations, Size imageSize)
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

    private async Task WriteAsync(RecordingSession session)
    {
        using var timer = new PeriodicTimer(this.flushInterval);
        List<OcrTraceFrame> pending = [];
        bool loggedFailure = false;

        while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
        {
            if (pending.Count == 0)
            {
                while (session.Frames.Reader.TryRead(out OcrTraceFrame? frame))
                {
                    pending.Add(frame);
                }
            }

            if (pending.Count > 0)
            {
                try
                {
                    byte[] chunk = BuildChunk(pending, session.CommittedLength == 0);
                    session.CommittedLength = await AppendChunkAsync(session.Path, session.CommittedLength, chunk)
                        .ConfigureAwait(false);
                    pending.Clear();
                    if (loggedFailure)
                    {
                        this.logger.LogInformation("OCR trace recording resumed: {Path}", session.Path);
                        loggedFailure = false;
                    }
                }
                catch (Exception error)
                {
                    if (!loggedFailure)
                    {
                        this.logger.LogError(error, "OCR trace write failed; retrying: {Path}", session.Path);
                        loggedFailure = true;
                    }
                }
            }

            if (session.Frames.Reader.Completion.IsCompleted && pending.Count == 0)
            {
                return;
            }
        }
    }

    private static byte[] BuildChunk(IReadOnlyList<OcrTraceFrame> pending, bool includeHeader)
    {
        StringBuilder lines = new();
        if (includeHeader)
        {
            lines.AppendLine(JsonSerializer.Serialize(new OcrTraceHeader(OcrTraceHeader.CurrentVersion)));
        }
        foreach (OcrTraceFrame frame in pending)
        {
            lines.AppendLine(JsonSerializer.Serialize(frame, JsonOptions));
        }
        return Encoding.UTF8.GetBytes(lines.ToString());
    }

    private async Task<long> AppendChunkAsync(string path, long committedLength, byte[] chunk)
    {
        Directory.CreateDirectory(this.directory);
        await using FileStream stream = new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length < committedLength)
        {
            throw new IOException("OCR trace file was shortened while recording.");
        }
        if (stream.Length > committedLength)
        {
            stream.SetLength(committedLength);
        }
        stream.Position = committedLength;
        await stream.WriteAsync(chunk).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
        return stream.Position;
    }

    public async ValueTask DisposeAsync()
    {
        Task[] tasks;
        lock (this.gate)
        {
            this.disposed = true;
            RecordingSession? session = this.current;
            Volatile.Write(ref this.current, null);
            session?.Frames.Writer.TryComplete();
            tasks = this.writerTasks.ToArray();
        }

#pragma warning disable VSTHRD003 // 専用ライターの残りのフレームを待つ
        await Task.WhenAll(tasks).ConfigureAwait(false);
#pragma warning restore VSTHRD003
    }

    private sealed class RecordingSession(string path)
    {
        public string Path { get; } = path;

        public Channel<OcrTraceFrame> Frames { get; } = Channel.CreateUnbounded<OcrTraceFrame>(
            new UnboundedChannelOptions { SingleReader = true });

        public long CommittedLength { get; set; }
    }
}
