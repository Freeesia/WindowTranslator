using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace WindowTranslator.Extensions;

/// <summary>
/// <see cref="IAsyncEnumerable{T}"/>の拡張メソッドを提供します。
/// </summary>
public static class AsyncEnumerable
{
    /// <summary>
    /// 並列に処理して終わった順に結果を返します。
    /// </summary>
    /// <typeparam name="TSource">処理の対象の型</typeparam>
    /// <typeparam name="TResult">処理の結果の型</typeparam>
    /// <param name="source">処理の対象のリスト</param>
    /// <param name="func">並列で行う処理</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>結果</returns>
    public static async IAsyncEnumerable<TResult> WhenEach<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, CancellationToken, ValueTask<TResult>> func, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<TResult>();
        using var completionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? task = null;
        try
        {
            task = Parallel.ForEachAsync(source, completionCts.Token, async (s, ct) =>
            {
                try
                {
                    var result = await func(s, ct).ConfigureAwait(false);
                    await channel.Writer.WriteAsync(result, ct).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    channel.Writer.Complete(e);
                }
            }).ContinueWith(static (_, s) => ((ChannelWriter<TResult>)s!).Complete(), channel.Writer, cancellationToken);

            await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return result;
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            completionCts.Cancel();
            try
            {
                if (task is { } t)
                {
                    await t.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }
    }
}