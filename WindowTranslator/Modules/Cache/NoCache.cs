using WindowTranslator.ComponentModel;
using WindowTranslator.Properties;

namespace WindowTranslator.Modules.Cache;

/// <summary>
/// キャッシュを使用しないキャッシュモジュールです。
/// </summary>
[LocalizedDisplayName(typeof(Resources), nameof(NoCache))]
public class NoCache : ICacheModule
{
    /// <inheritdoc />
    public bool Contains(string src) => false;

    /// <inheritdoc />
    public void AddRange(IEnumerable<(string src, string dst)> pairs) { }

    /// <inheritdoc />
    public string Get(string src) => string.Empty;
}