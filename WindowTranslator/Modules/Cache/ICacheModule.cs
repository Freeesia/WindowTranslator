using System.Collections.Generic;

namespace WindowTranslator.Modules.Cache;

public interface ICacheModule
{
    bool Contains(string src);
    void AddRange(IEnumerable<(string src, string dst)> pairs);
    string Get(string src);
}
