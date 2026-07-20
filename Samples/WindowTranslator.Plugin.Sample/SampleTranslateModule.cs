using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.Sample;

/// <summary>
/// サンプル翻訳モジュールです。
/// テキストをそのまま返す（翻訳しない）実装例です。
/// </summary>
[DisplayName("サンプル翻訳")]
public class SampleTranslateModule : ITranslateModule
{
    private readonly ILogger<SampleTranslateModule> logger;

    public SampleTranslateModule(ILogger<SampleTranslateModule> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> TranslateAsync(
        IAsyncEnumerable<string> texts,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var text in texts.WithCancellation(cancellationToken))
        {
            this.logger.LogDebug("翻訳: {Text}", text);
            // TODO: ここで実際の翻訳処理を実装してください
            yield return $"[翻訳済み] {text}";
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
