namespace WindowTranslator;

/// <summary>
/// ユーザー設定
/// </summary>
public class UserSettings
{
    /// <summary>
    /// 共通設定
    /// </summary>
    public CommonSettings Common { get; init; } = new();

    /// <summary>
    /// 翻訳対象ごとの設定
    /// </summary>
    public Dictionary<string, TargetSettings> Targets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 共通設定
/// </summary>
public class CommonSettings
{
    /// <summary>
    /// 翻訳結果の表示モード
    /// </summary>
    public ViewMode ViewMode { get; set; } = ViewMode.Overlay;

    /// <summary>
    /// キャプチャー可能にするか
    /// </summary>
    public bool IsEnableCaptureOverlay { get; set; }

    /// <summary>
    /// オーバーレイの切り替え方法
    /// </summary>
    public OverlaySwitch OverlaySwitch { get; set; } = OverlaySwitch.Hold;

    /// <summary>
    /// 自動的に翻訳を発動するか
    /// </summary>
    public bool IsEnableAutoTarget { get; set; }
}

/// <summary>
/// 翻訳対象ごとの設定
/// </summary>
public class TargetSettings
{
    /// <summary>
    /// 翻訳言語のオプション
    /// </summary>
    public LanguageOptions Language { get; init; } = new();

    /// <summary>
    /// 翻訳結果を表示するためのフォント
    /// </summary>
    public string Font { get; set; } = "Yu Gothic UI";

    /// <summary>
    /// フォントの拡大率
    /// </summary>
    public double FontScale { get; set; } = 1.1;

    /// <summary>
    /// オーバーレイ表示の切り替えショートカットキー
    /// </summary>
    public string OverlayShortcut { get; set; } = "Ctrl + Alt + O";

    /// <summary>
    /// 処理中の表示を行うか
    /// </summary>
    public bool DisplayBusy { get; set; } = true;

    /// <summary>
    /// プラグインの選択
    /// </summary>
    public Dictionary<string, string> SelectedPlugins { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// プラグインのパラメータ
    /// </summary>
    public Dictionary<string, IPluginParam> PluginParams { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 翻訳結果の表示モード
/// </summary>
public enum ViewMode
{
    /// <summary>
    /// キャプチャー
    /// </summary>
    Capture,

    /// <summary>
    /// オーバーレイ
    /// </summary>
    Overlay,
}

/// <summary>
/// オーバレイ表示の切り替え方法
/// </summary>
public enum OverlaySwitch
{
    /// <summary>
    /// ホールド
    /// </summary>
    Hold,

    /// <summary>
    /// トグル
    /// </summary>
    Toggle,
}