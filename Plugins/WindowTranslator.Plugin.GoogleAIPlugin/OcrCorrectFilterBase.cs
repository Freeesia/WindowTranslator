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
    private readonly bool waitCorrect;
    private bool disposedValue;

    protected ConcurrentDictionary<string, string?> Cache { get; } = new();
    protected ILogger Logger { get; }

    protected GenerativeModel Client => this.client ?? throw new InvalidOperationException();

    protected abstract CorrectMode TargetMode { get; }

    protected OcrCorrectFilterBase(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GoogleAIOptions> googleAiOptions, ILogger logger)
    {
        var options = googleAiOptions.Value;
        this.Logger = logger;
        this.waitCorrect = options.WaitCorrect;
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
        Task.Run(LoopCorrect, this.cts.Token);
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

        // 全てのテキストを収集し、キャッシュを確認
        await foreach (var text in texts.ConfigureAwait(false))
        {
            if (!this.Cache.TryGetValue(text.SourceText, out var corrected))
            {
                targets.Add(text);
            }
            // キャッシュに存在する場合は、補正済みのテキストを返す
            if (corrected is not null)
            {
                yield return text.SourceText != corrected ? text with { SourceText = corrected } : text;
            }
            // WaitCorrectが無効な場合は、補正を待たずにそのまま返す
            else if (!this.waitCorrect)
            {
                yield return text;
            }
        }

        // 補正が必要なテキストの処理
        if (targets.Count > 0)
        {
            var queueData = await GetQueueData(targets, context).ConfigureAwait(false);

            // WaitCorrectが有効な場合：補正処理を完了してから結果を返す
            if (this.waitCorrect)
            {
                await Correct(queueData, this.cts.Token).ConfigureAwait(false);
                // 補正後のテキストをキャッシュから返す
                foreach (var text in targets)
                {
                    // キャッシュに必ず存在するはずだけど、念のためなかったらそのまま返す
                    yield return this.Cache.TryGetValue(text.SourceText, out var corrected) && corrected is not null && text.SourceText != corrected
                        ? (text with { SourceText = corrected })
                        : text;
                }
            }
            // WaitCorrectが無効な場合：従来の遅延処理
            else
            {
                await this.queue.Writer.WriteAsync(queueData, this.cts.Token).ConfigureAwait(false);
            }
        }
    }

    protected abstract ValueTask<IReadOnlyList<T>> GetQueueData(IEnumerable<TextRect> targets, FilterContext context);

    private async Task LoopCorrect()
    {
        await foreach (var texts in this.queue.Reader.ReadAllAsync(this.cts.Token))
        {
            await Correct(texts, this.cts.Token).ConfigureAwait(false);
        }
    }

    private async Task Correct(IReadOnlyList<T> texts, CancellationToken token)
    {
        while (true)
        {
            try
            {
                await CorrectCore(texts, token).ConfigureAwait(false);
                break;
            }
            catch (ApiException e) when (e.ErrorCode == 400)
            {
                throw new AppUserException("GeminiのAPIキーが無効です。設定ダイアログからGoogleAIオプションを設定してください", e);
            }
            // サービスが一時的に過負荷になっているか、ダウンしている可能性があります。
            catch (ApiException e) when (e.ErrorCode == 503)
            {
                this.Logger.LogWarning("Geminiのサービスが一時的に過負荷になっているか、ダウンしている可能性があります。500ミリ秒待機して再試行します。");
                await Task.Delay(500, token).ConfigureAwait(false);
                continue;
            }
            // レート制限を超えました。
            catch (ApiException e) when (e.ErrorCode == 429)
            {
                this.Logger.LogWarning("Geminiのレート制限を超えました。10秒待機して再試行します。");
                await Task.Delay(10000, token).ConfigureAwait(false);
                continue;
            }
            // Jsonエラーということは指定した以外のレスポンスが返ってきたのでもう一度
            catch (JsonException)
            {
                continue;
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
