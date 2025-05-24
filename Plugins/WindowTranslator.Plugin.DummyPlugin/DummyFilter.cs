using Microsoft.Extensions.Logging;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.DummyPlugin;
public class DummyFilter(ILogger<DummyFilter> logger) : IFilterModule
{
    private readonly ILogger<DummyFilter> logger = logger;

    public IAsyncEnumerable<TextRect> ExecutePostTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
    {
        this.logger.LogDebug("ExecutePostTranslate");
        return texts;
    }

    public IAsyncEnumerable<TextRect> ExecutePreTranslate(IAsyncEnumerable<TextRect> texts, FilterContext context)
    {
        this.logger.LogDebug("ExecutePreTranslate");
        return texts;
    }
}
