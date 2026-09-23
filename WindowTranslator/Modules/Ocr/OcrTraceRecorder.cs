using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace WindowTranslator.Modules.Ocr;

public sealed class OcrTraceOptions
{
    public bool Enabled { get; set; }

    public string? Directory { get; set; }
}

/// <summary>
/// OCRの呼び出し元を待たせず、フレームを別スレッドで追記する。
/// </summary>
public sealed class OcrTraceRecorder : IAsyncDisposable
{
    private const int QueueCapacity = 256;
    private readonly Channel<OcrTraceFrame>? channel;
    private readonly Task? writerTask;
    private readonly ILogger<OcrTraceRecorder> logger;
    private readonly string directory;
    private readonly TimeSpan origin = CurrentTimestamp();
    private long frameNumber;
    private long droppedFrames;
    private int active;

    public bool IsEnabled => Volatile.Read(ref this.active) == 1;

    public OcrTraceRecorder(OcrTraceOptions options, ILogger<OcrTraceRecorder> logger)
    {
        this.logger = logger;
        this.directory = string.IsNullOrWhiteSpace(options.Directory)
            ? Path.Combine(PathUtility.UserDir, "ocr-traces")
            : options.Directory;
        if (!options.Enabled)
        {
            return;
        }
        this.channel = Channel.CreateBounded<OcrTraceFrame>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        this.active = 1;
        this.writerTask = Task.Run(WriteAsync);
    }

    public void Record(IReadOnlyList<TextRect> observations, Size imageSize)
    {
        if (!this.IsEnabled || this.channel is null)
        {
            return;
        }

        try
        {
            TimeSpan timestamp = CurrentTimestamp();
            OcrTraceFrame frame = new(
                OcrTraceFrame.CurrentVersion,
                Interlocked.Increment(ref this.frameNumber),
                (timestamp - this.origin).Ticks,
                imageSize.Width,
                imageSize.Height,
                observations.Select(OcrTraceRect.FromTextRect).ToArray());
            if (!this.channel.Writer.TryWrite(frame))
            {
                Interlocked.Increment(ref this.droppedFrames);
            }
        }
        catch (Exception error)
        {
            this.logger.LogWarning(error, "OCR trace frame could not be queued.");
        }

    }

    private async Task WriteAsync()
    {
        if (this.channel is null)
        {
            return;
        }

        try
        {
            await using var enumerator = this.channel.Reader.ReadAllAsync().GetAsyncEnumerator();
            if (!await enumerator.MoveNextAsync())
            {
                return;
            }

            Directory.CreateDirectory(this.directory);
            string path = Path.Combine(this.directory,
                $"ocr-{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}-{Guid.NewGuid():N}.jsonl");
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
                FileShare.Read, 4096, FileOptions.Asynchronous);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false))
            {
                AutoFlush = true,
            };
            this.logger.LogInformation("OCR trace recording to {Path}. OCR text and geometry are saved; images are not saved. Review the file before sharing it.", path);

            do
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(enumerator.Current));
            }
            while (await enumerator.MoveNextAsync());
        }
        catch (Exception error)
        {
            Volatile.Write(ref this.active, 0);
            this.channel.Writer.TryComplete();
            while (this.channel.Reader.TryRead(out _))
            {
            }
            this.logger.LogWarning(error, "OCR trace recording stopped. OCR and translation will continue.");
        }
        finally
        {
            long dropped = Interlocked.Read(ref this.droppedFrames);
            if (dropped > 0)
            {
                this.logger.LogWarning("OCR trace omitted {Count} frames because the writer could not keep up or recording stopped.", dropped);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        Volatile.Write(ref this.active, 0);
        this.channel?.Writer.TryComplete();
        if (this.writerTask is not null)
        {
#pragma warning disable VSTHRD003 // Task.Runで開始した専用ライターの終了を待つ
            await this.writerTask.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        }
    }

    private static TimeSpan CurrentTimestamp()
        => TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
}
