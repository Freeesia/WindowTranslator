namespace WindowTranslator;
public class UserSettings
{
    public LanguageOptions Language { get; } = new();
    public Dictionary<string, string> SelectedPlugins { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, IPluginParam> PluginParams { get; } = new(StringComparer.OrdinalIgnoreCase);
}
