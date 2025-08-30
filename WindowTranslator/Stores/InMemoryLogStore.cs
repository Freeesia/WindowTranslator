using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace WindowTranslator.Stores;

public class InMemoryLogStore : ILogStore
{
    private readonly ConcurrentQueue<LogEntry> logs = new();
    private readonly int _maxLogCount = 10000; // 最大ログ数を制限
    private int _currentCount;

    public event EventHandler<LogEntry>? LogAdded;

    public void AddLog(LogEntry logEntry)
    {
        logs.Enqueue(logEntry);

        // ログ数の制限を適用
        if (Interlocked.Increment(ref _currentCount) > _maxLogCount)
        {
            if (logs.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _currentCount);
            }
        }

        LogAdded?.Invoke(this, logEntry);
    }

    public IReadOnlyList<LogEntry> GetLogs()
        => [.. logs];

    public void Clear()
    {
        while (logs.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _currentCount);
        }
    }
}
public interface ILogStore
{
    IReadOnlyList<LogEntry> GetLogs();
    void Clear();
    void AddLog(LogEntry logEntry);

    event EventHandler<LogEntry>? LogAdded;
}

public record LogEntry(DateTime Timestamp, LogLevel Level, string Category, string Message, Exception? Exception = null)
{
    public string FormattedMessage => Exception is null ? Message : $"{Message}: {Exception}";
}
