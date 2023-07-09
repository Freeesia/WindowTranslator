using System.ComponentModel;

namespace WindowTranslator;
public class UserSettings
{
    public LanguageOptions Language { get; init; } = new();

    public ViewMode ViewMode { get; set; } = ViewMode.Capture;

    public IList<string> AutoTargets { get; set; } = new List<string>();

    public bool IsEnableAutoTarget { get; set; }

    public Dictionary<string, string> SelectedPlugins { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, IPluginParam> PluginParams { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum ViewMode
{
    [Description(nameof(Capture))]
    Capture,
    [Description(nameof(Overlay))]
    Overlay,
}