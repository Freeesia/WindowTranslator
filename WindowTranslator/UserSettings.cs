namespace WindowTranslator;
public class UserSettings
{
    public LanguageOptions Language { get; init; } = new();
    public Dictionary<string, string> SelectedPlugins { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, IPluginParam> PluginParams { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
