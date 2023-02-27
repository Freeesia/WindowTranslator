namespace WindowTranslator.Modules;

public interface ICacheModule
{
    bool Contains(string src);
    void AddRange(IEnumerable<(string src, string dst)> pairs);
    string Get(string src);
}
