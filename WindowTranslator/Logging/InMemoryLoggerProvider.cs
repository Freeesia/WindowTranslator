using Microsoft.Extensions.Logging;
using WindowTranslator.Stores;

namespace WindowTranslator.Logging;

public sealed class InMemoryLoggerProvider(ILogStore loggerService) : ILoggerProvider
{
    private readonly ILogStore loggerStore = loggerService;

    public ILogger CreateLogger(string categoryName)
        => new InMemoryLogger(categoryName, loggerStore);

    public void Dispose()
    {
        // Nothing to dispose
    }
}

public class InMemoryLogger(string categoryName, ILogStore loggerStore) : ILogger
{
    private readonly string categoryName = categoryName;
    private readonly ILogStore loggerStore = loggerStore;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var logEntry = new LogEntry(
            DateTime.Now,
            logLevel,
            categoryName,
            message,
            exception);

        loggerStore.AddLog(logEntry);
    }
}

