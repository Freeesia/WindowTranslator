using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.LLMPlugin;

public abstract class OcrCorrectFilterBase<T> : IFilterModule, IDisposable
{
    private readonly Channel<IReadOnlyList<T>> queue;
    private readonly CancellationTokenSource cts = new();
    private readonly ChatClient? client;
    private bool disposedValue;

    protected ConcurrentDictionary<string, string?> Cache { get; } = new();
    protected ILogger Logger { get; }

    protected ChatClient Client => this.client ?? throw new InvalidOperationException();

    protected abstract CorrectMode TargetMode { get; }

    protected OcrCorrectFilterBase(IOptionsSnapshot<LLMOptions> llmOptions, ILogger logger)
    {
        var options = llmOptions.Value;
        this.Logger = logger;
        this.queue = Channel.CreateBounded<IReadOnlyList<T>>(new(1)
        {
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        }, Dropped);
        if (string.IsNullOrEmpty(options.ApiKey) || this.TargetMode != options.CorrectMode || string.IsNullOrEmpty(options.Model))
        {
            return;
        }
        this.client = new(
            options.Model,
            new(options.ApiKey),
            options.Endpoint is { Length: > 0 } e ? new OpenAIClientOptions() { Endpoint = new(e) } : null);
        Task.Run(Correct, this.cts.Token);
    }

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
        => texts;

    public async IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
    {
        if (this.client is null)
        {
            await foreach (var text in texts.ConfigureAwait(false))
            {
                yield return text;
            }
            yield break;
        }
        var targets = new List<TextRect>();
        await foreach (var text in texts.ConfigureAwait(false))
        {
            if (!this.Cache.TryGetValue(text.Text, out var corrected))
            {
                targets.Add(text);
            }
            if (corrected is not null)
            {
                yield return text.Text != corrected ? text with { Text = corrected } : text;
            }
            else
            {
                yield return text;
            }
        }

        if (targets.Count > 0)
        {
            await this.queue.Writer.WriteAsync(
                await GetQueueData(targets, context).ConfigureAwait(false),
                this.cts.Token)
                .ConfigureAwait(false);
        }
    }

    protected abstract ValueTask<IReadOnlyList<T>> GetQueueData(IEnumerable<TextRect> targets, FilterContext context);

    private async Task Correct()
    {
        await foreach (var texts in this.queue.Reader.ReadAllAsync(this.cts.Token))
        {
            await CorrectCore(texts, this.cts.Token).ConfigureAwait(false);
        }
    }

    protected abstract Task CorrectCore(IReadOnlyList<T> texts, CancellationToken cancellationToken);

    protected abstract void Dropped(IReadOnlyList<T> texts);

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                this.queue.Writer.Complete();
                this.cts.Cancel();
            }

            // MEMO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
            // MEMO: 大きなフィールドを null に設定します
            disposedValue = true;
        }
    }

    // // MEMO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
    // ~OcrCorrectFilterBase()
    // {
    //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
