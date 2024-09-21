using WindowTranslator.ComponentModel;
using WindowTranslator.Properties;

namespace WindowTranslator;
public class UserSettings
{
    public LanguageOptions Language { get; init; } = new();

    public string Font { get; set; } = "Yu Gothic UI";
    public double FontScale { get; set; } = 1.1;

    public ViewMode ViewMode { get; set; } = ViewMode.Capture;

    public IList<string> AutoTargets { get; set; } = [];

    public bool IsEnableAutoTarget { get; set; }

    public OverlaySwitch OverlaySwitch { get; set; } = OverlaySwitch.Hold;

    public bool IsEnableCaptureOverlay { get; set; }

    public Dictionary<string, string> SelectedPlugins { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, IPluginParam> PluginParams { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public enum ViewMode
{
    [LocalizedDescription(typeof(Resources), nameof(Capture))]
    Capture,
    [LocalizedDescription(typeof(Resources), nameof(Overlay))]
    Overlay,
}

public enum OverlaySwitch
{
    [LocalizedDescription(typeof(Resources), nameof(Hold))]
    Hold,
    [LocalizedDescription(typeof(Resources), nameof(Toggle))]
    Toggle,
}