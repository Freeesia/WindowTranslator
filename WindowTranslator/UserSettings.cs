
using PropertyTools.DataAnnotations;

namespace WindowTranslator;
public class UserSettings
{
    public LanguageOptions Language { get; init; } = new();

    public ViewMode ViewMode { get; set; } = ViewMode.Capture;

    public Dictionary<string, string> SelectedPlugins { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, IPluginParam> PluginParams { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum ViewMode
{
    Capture,
    Overlay,
}