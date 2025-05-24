using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Extensions;

public static class LoggerExtensions
{
    public static IDisposable LogDebugTime(this ILogger logger, string message)
    {
        var sw = Stopwatch.StartNew();
        return new DisposeAction(() => logger.LogDebug($"{message}: {sw.Elapsed}"));
    }
}
