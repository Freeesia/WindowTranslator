using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using GenerativeAI;
using GenerativeAI.Exceptions;
using GenerativeAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public abstract class OcrCorrectFilterBase<T> : IFilterModule, IDisposable
{
    private readonly Channel<IReadOnlyList<T>> queue;
    private readonly CancellationTokenSource cts = new();
    private readonly GenerativeModel? client;
    private bool disposedValue;

    protected ConcurrentDictionary<string, string?> Cache { get; } = new();
    protected ILogger Logger { get; }

    protected GenerativeModel Client => this.client ?? throw new InvalidOperationException();

    protected abstract CorrectMode TargetMode { get; }

    protected OcrCorrectFilterBase(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GoogleAIOptions> googleAiOptions, ILogger logger)
    {
        var options = googleAiOptions.Value;
        this.Logger = logger;
        this.queue = Channel.CreateBounded<IReadOnlyList<T>>(new(1)
        {
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        }, Dropped);
        if (string.IsNullOrEmpty(options.ApiKey) || this.TargetMode != googleAiOptions.Value.CorrectMode)
        {
            return;
        }
        var googleAI = new GoogleAi(options.ApiKey, logger: logger);
        this.client = googleAI.CreateGenerativeModel(
            string.IsNullOrEmpty(options.PreviewModel) ? options.Model.GetName() : options.PreviewModel,
            safetyRatings: [
                new(){ Category = HarmCategory.HARM_CATEGORY_HARASSMENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_HATE_SPEECH, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_SEXUALLY_EXPLICIT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_DANGEROUS_CONTENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
            ],
            systemInstruction: GetSystem(langOptions.Value, options));
        Task.Run(Correct, this.cts.Token);
    }

    protected abstract string GetSystem(LanguageOptions languageOptions, GoogleAIOptions googleAIOptions);

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
            while (true)
            {
                try
                {
                    await CorrectCore(texts, this.cts.Token).ConfigureAwait(false);
                    break;
                }
                catch (ApiException e) when (e.ErrorCode == 400)
                {
                    throw new ApiException(e.ErrorCode, "GoogleAIのAPIキーが無効です。設定ダイアログからGoogleAIオプションを設定してください", e.ErrorStatus);
                }
                // サービスが一時的に過負荷になっているか、ダウンしている可能性があります。
                catch (ApiException e) when (e.ErrorCode == 503)
                {
                    this.Logger.LogWarning("GoogleAIのサービスが一時的に過負荷になっているか、ダウンしている可能性があります。500ミリ秒待機して再試行します。");
                    await Task.Delay(500).ConfigureAwait(false);
                    continue;
                }
                // レート制限を超えました。
                catch (ApiException e) when (e.ErrorCode == 429)
                {
                    this.Logger.LogWarning("GoogleAIのレート制限を超えました。10秒待機して再試行します。");
                    await Task.Delay(10000).ConfigureAwait(false);
                    continue;
                }
                // Jsonエラーということは指定した以外のレスポンスが返ってきたのでもう一度
                catch (JsonException)
                {
                    continue;
                }
            }
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
