using System.Collections.Concurrent;
using R3;
using WindowTranslator.Extensions;

namespace WindowTranslator.Tests;

public class ObservableExtensionsTests
{
    [Fact]
    public async Task 処理中に届いた値は最後の値だけを実行する()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(processor.ProcessAsync, Discard, configureAwait: false);
        var first = new TestValue(1);
        var replaced = new TestValue(2);
        var last = new TestValue(3);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(replaced);
        source.OnNext(last);
        last.Release();
        first.Release();
        await last.WaitUntilDisposedAsync();

        Assert.Equal([1, 3], processor.StartedIds.ToArray());
    }

    [Fact]
    public async Task 差し替えられた待機値は一度だけ破棄する()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(processor.ProcessAsync, Discard, configureAwait: false);
        var first = new TestValue(1);
        var replaced = new TestValue(2);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(replaced);
        source.OnNext(new TestValue(3));
        subscription.Dispose();

        Assert.Equal(1, replaced.DisposeCount);
    }

    [Fact]
    public async Task 待機値を差し替えても実行中の値は破棄しない()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(processor.ProcessAsync, Discard, configureAwait: false);
        var first = new TestValue(1);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(new TestValue(2));
        source.OnNext(new TestValue(3));

        Assert.Equal(0, first.DisposeCount);
    }

    [Fact]
    public async Task 購読解除で待機値を一度だけ破棄する()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(processor.ProcessAsync, Discard, configureAwait: false);
        var first = new TestValue(1);
        var pending = new TestValue(2);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(pending);
        subscription.Dispose();
        subscription.Dispose();

        Assert.Equal(1, pending.DisposeCount);
    }

    [Fact]
    public async Task 購読解除で実行中の処理をキャンセルする()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(processor.ProcessAsync, Discard, configureAwait: false);
        var first = new TestValue(1);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        subscription.Dispose();
        await first.WaitUntilDisposedAsync();

        Assert.True(first.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task 通常完了では最後の待機値まで処理する()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(processor.ProcessAsync, Discard, configureAwait: false);
        var first = new TestValue(1);
        var last = new TestValue(2);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(last);
        source.OnCompleted();
        last.Release();
        first.Release();
        await last.WaitUntilDisposedAsync();

        Assert.Equal([1, 2], processor.StartedIds.ToArray());
    }

    [Fact]
    public async Task 完了時キャンセルを指定すると待機値を破棄する()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(
            processor.ProcessAsync, Discard, configureAwait: false, cancelOnCompleted: true);
        var first = new TestValue(1);
        var pending = new TestValue(2);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(pending);
        source.OnCompleted();

        Assert.Equal(1, pending.DisposeCount);
    }

    [Fact]
    public async Task 失敗による完了では待機値を破棄する()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        var errors = new ConcurrentQueue<Exception>();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(
            processor.ProcessAsync, Discard, configureAwait: false, onErrorResume: errors.Enqueue);
        var first = new TestValue(1);
        var pending = new TestValue(2);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(pending);
        source.OnCompleted(new InvalidOperationException());

        Assert.Equal(1, pending.DisposeCount);
    }

    [Fact]
    public async Task 処理の例外を通知して次の待機値を処理する()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        var errors = new ConcurrentQueue<Exception>();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(
            processor.ProcessAsync, Discard, configureAwait: false, onErrorResume: errors.Enqueue);
        var first = new TestValue(1);
        var last = new TestValue(2);
        var error = new InvalidOperationException();

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(last);
        last.Release();
        first.Fail(error);
        await last.WaitUntilDisposedAsync();

        Assert.Same(error, Assert.Single(errors));
    }

    [Fact]
    public async Task 処理がキャンセルされた場合も残った待機値を破棄する()
    {
        using var source = new Subject<TestValue>();
        var processor = new Processor();
        using var subscription = source.SubscribeAwaitThrottleFirstLast(processor.ProcessAsync, Discard, configureAwait: false);
        var first = new TestValue(1);
        var pending = new TestValue(2);

        source.OnNext(first);
        await first.WaitUntilStartedAsync();
        source.OnNext(pending);
        first.Fail(new OperationCanceledException());
        await pending.WaitUntilDisposedAsync();

        Assert.Equal(1, pending.DisposeCount);
    }

    private static void Discard(TestValue value) => value.Dispose();

    private sealed class Processor
    {
        public ConcurrentQueue<int> StartedIds { get; } = new();

        public async ValueTask ProcessAsync(TestValue value, CancellationToken cancellationToken)
        {
            using (value)
            {
                this.StartedIds.Enqueue(value.Id);
                value.Start(cancellationToken);
                await value.WaitUntilReleasedAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class TestValue(int id) : IDisposable
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int disposeCount;

        public int Id { get; } = id;
        public int DisposeCount => Volatile.Read(ref this.disposeCount);
        public CancellationToken CancellationToken { get; private set; }

        public void Start(CancellationToken cancellationToken)
        {
            this.CancellationToken = cancellationToken;
            this.started.TrySetResult();
        }

        public void Release() => this.released.TrySetResult();
        public void Fail(Exception error) => this.released.TrySetException(error);
        public Task WaitUntilStartedAsync() => this.started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public Task WaitUntilDisposedAsync() => this.disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        public Task WaitUntilReleasedAsync(CancellationToken cancellationToken) => this.released.Task.WaitAsync(cancellationToken);

        public void Dispose()
        {
            Interlocked.Increment(ref this.disposeCount);
            this.disposed.TrySetResult();
        }
    }
}
