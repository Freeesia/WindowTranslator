namespace WindowTranslator;

/// <summary>
/// パスのユーティリティクラスです。
/// </summary>
public static class PathUtility
{
    /// <summary>
    /// ユーザー設定ディレクトリのパスを取得します。
    /// </summary>
    public static readonly string UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wt");

    /// <summary>
    /// ユーザー設定ファイルのパスを取得します。
    /// </summary>
    public static readonly string UserSettings = Path.Combine(UserDir, "settings.json");
}
