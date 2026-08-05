using System.Globalization;
using System.Resources;
using WindowTranslator.Modules;

namespace WindowTranslator.Controls;

/// <summary>
/// 優先矩形UIで利用する文字列リソース
/// </summary>
/// <remarks>
/// 優先矩形の設定項目と同じ<see cref="WindowTranslator.Abstractions"/>のリソースを参照する
/// </remarks>
public static class PriorityRectResources
{
    private static readonly ResourceManager? resourceManager = typeof(BasicOcrParam).GetResourceManager();

    /// <summary>矩形を追加</summary>
    public static string Add => GetString(nameof(Add));

    /// <summary>矩形を削除</summary>
    public static string Remove => GetString(nameof(Remove));

    /// <summary>上へ移動</summary>
    public static string MoveUp => GetString(nameof(MoveUp));

    /// <summary>下へ移動</summary>
    public static string MoveDown => GetString(nameof(MoveDown));

    /// <summary>キーワード</summary>
    public static string Keyword => GetString(nameof(Keyword));

    /// <summary>キーワードの説明</summary>
    public static string KeywordDescription => GetString(nameof(KeywordDescription));

    /// <summary>矩形選択</summary>
    public static string Selection => GetString(nameof(Selection));

    /// <summary>矩形選択の操作説明</summary>
    public static string SelectionGuide => GetString(nameof(SelectionGuide));

    /// <summary>選択中</summary>
    public static string Selecting => GetString(nameof(Selecting));

    /// <summary>矩形が小さすぎる場合の警告</summary>
    public static string TooSmall => GetString(nameof(TooSmall));

    /// <summary>対象ウィンドウが翻訳中でない場合の説明</summary>
    public static string TargetNotFound => GetString(nameof(TargetNotFound));

    private static string GetString(string name)
        => resourceManager?.GetString($"PriorityRect{name}", CultureInfo.CurrentUICulture) ?? name;
}
