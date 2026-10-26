using System.Threading.Channels;
using R3;

namespace WindowTranslator.Extensions;

public static class ObservableExtensions
{
    /// <summary>
    /// 先頭と処理中に届いた最新の値を順次処理し、処理されない待機値を破棄します。
    /// </summary>
    /// <remarks>
    /// 処理を開始した値の後始末は onNextAsync、差し替え・購読終了で不要になった値の後始末は onDiscard が担当します。
    /// </remarks>
    public static IDisposable SubscribeAwaitThrottleFirstLast<T>(
        this Observable<T> source,
        Func<T, CancellationToken, ValueTask> onNextAsync,
        Action<T> onDiscard,
        bool configureAwait = true,
        bool cancelOnCompleted = false,
        Action<Exception>? onErrorResume = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onNextAsync);
        ArgumentNullException.ThrowIfNull(onDiscard);

        return source.Subscribe(new ThrottleFirstLastObserver<T>(
            onNextAsync, onDiscard, configureAwait, cancelOnCompleted,
            onErrorResume ?? ObservableSystem.GetUnhandledExceptionHandler()));
    }

    private sealed class ThrottleFirstLastObserver<T> : Observer<T>
    {
        private readonly Func<T, CancellationToken, ValueTask> onNextAsync;
        private readonly Action<T> onDiscard;
        private readonly Action<Exception> onErrorResume;
        private readonly bool configureAwait;
        private readonly bool cancelOnCompleted;
        private readonly Channel<T> channel;
        private readonly IDisposable worker;

        protected override bool AutoDisposeOnCompleted => false;

        public ThrottleFirstLastObserver(
            Func<T, CancellationToken, ValueTask> onNextAsync,
            Action<T> onDiscard,
            bool configureAwait,
            bool cancelOnCompleted,
            Action<Exception> onErrorResume)
        {
            this.onNextAsync = onNextAsync;
            this.onDiscard = onDiscard;
            this.onErrorResume = onErrorResume;
            this.configureAwait = configureAwait;
            this.cancelOnCompleted = cancelOnCompleted;
            this.channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            }, onDiscard);
            this.worker = Observable.FromAsync(RunAsync, configureAwait)
                .Subscribe(_ => { }, onErrorResume, Complete);
        }

        protected override void OnNextCore(T value)
        {
            if (!this.channel.Writer.TryWrite(value))
            {
                this.onDiscard(value);
            }
        }

        protected override void OnErrorResumeCore(Exception error) => this.onErrorResume(error);

        protected override void OnCompletedCore(Result result)
        {
            this.channel.Writer.TryComplete();
            if (this.cancelOnCompleted || result.IsFailure)
            {
                Complete(result);
            }
        }

        protected override void DisposeCore()
        {
            this.channel.Writer.TryComplete();
            this.worker.Dispose();
            while (this.channel.Reader.TryRead(out var value))
            {
                this.onDiscard(value);
            }
        }

        private void Complete(Result result)
        {
            try
            {
                if (result.Exception is { } error)
                {
                    this.onErrorResume(error);
                }
            }
            finally
            {
                Dispose();
            }
        }

        private async ValueTask RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (await this.channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(this.configureAwait))
                {
                    while (this.channel.Reader.TryRead(out var value))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            this.onDiscard(value);
                            return;
                        }

                        try
                        {
                            await this.onNextAsync(value, cancellationToken).ConfigureAwait(this.configureAwait);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        // 購読コールバックの例外は R3 と同様に通知し、次の待機値の処理を続ける。
                        catch (Exception error) when (error is not OperationCanceledException)
                        {
                            this.onErrorResume(error);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 購読解除による終了では、待機値の破棄を DisposeCore が担当する。
            }
        }
    }
}
