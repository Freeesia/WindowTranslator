using System.Text;
using System.Windows.Input;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace WindowTranslator.Extensions;

/// <summary>
/// キーの文字列表現と<see cref="Key"/>オブジェクト間の相互変換を行うユーティリティクラス
/// </summary>
public static class KeyExtensions
{
    /// <summary>
    /// キー文字列を<see cref="ShortcutKeySettings"/>に変換します
    /// </summary>
    /// <param name="keyString">「Ctrl + Alt + O」のような形式のキー文字列</param>
    /// <returns>変換されたShortcutKeySettings</returns>
    public static (ModifierKeys Modifiers, Key Key) ToShortcutKey(this string keyString)
    {

        if (string.IsNullOrWhiteSpace(keyString))
            return (ModifierKeys.None, Key.None);

        var key = default(Key);
        var modifires = default(ModifierKeys);
        var parts = keyString.Split('+', StringSplitOptions.TrimEntries);

        // メインキーは最後の部分
        if (parts.Length > 0)
        {
            key = Enum.Parse<Key>(parts[^1], true);
        }

        // 修飾キーをチェック
        foreach (var part in parts.Take(parts.Length - 1))
        {
            modifires |= part.ToLowerInvariant() switch
            {
                "ctrl" => ModifierKeys.Control,
                "alt" => ModifierKeys.Alt,
                "shift" => ModifierKeys.Shift,
                "win" => ModifierKeys.Windows,
                "windows" => ModifierKeys.Windows,
                _ => ModifierKeys.None,
            };
        }

        return (modifires, key);
    }

    /// <summary>
    /// <see cref="ModifierKeys"/>、<see cref="Key"/>のペアをキー文字列に変換します
    /// </summary>
    /// <param name="shortcut">変換する<see cref="Key"/></param>
    /// <returns>「Ctrl + Alt + O」のような形式のキー文字列</returns>
    public static string ToShortcutString(this (ModifierKeys Modifiers, Key Key) shortcut)
    {
        var sb = new StringBuilder();
        var (m, k) = shortcut;

        // 修飾キー
        if (m.HasFlag(ModifierKeys.Control))
        {
            sb.Append("Ctrl + ");
        }

        if (m.HasFlag(ModifierKeys.Alt))
        {
            sb.Append("Alt + ");
        }

        if (m.HasFlag(ModifierKeys.Shift))
        {
            sb.Append("Shift + ");
        }

        if (m.HasFlag(ModifierKeys.Windows))
        {
            sb.Append("Win + ");
        }

        // メインキー
        if (k.Or(Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin, Key.System))
        {
            sb.Append(Key.None.ToString());
        }
        else
        {
            sb.Append(k.ToString());
        }

        return sb.ToString();
    }

    internal static (HOT_KEY_MODIFIERS Modifiers, int Key) ToHotKey(this string keyString)
    {

        if (string.IsNullOrWhiteSpace(keyString))
            return (0, 0);

        var key = 0;
        var modifires = default(HOT_KEY_MODIFIERS);
        var parts = keyString.Split('+', StringSplitOptions.TrimEntries);

        // メインキーは最後の部分
        if (parts.Length > 0)
        {
            var tmp = Enum.Parse<Key>(parts[^1], true);
            key = KeyInterop.VirtualKeyFromKey(tmp);
        }

        // 修飾キーをチェック
        foreach (var part in parts.Take(parts.Length - 1))
        {
            modifires |= part.ToLowerInvariant() switch
            {
                "ctrl" => HOT_KEY_MODIFIERS.MOD_CONTROL,
                "alt" => HOT_KEY_MODIFIERS.MOD_ALT,
                "shift" => HOT_KEY_MODIFIERS.MOD_SHIFT,
                "win" => HOT_KEY_MODIFIERS.MOD_WIN,
                "windows" => HOT_KEY_MODIFIERS.MOD_WIN,
                _ => 0,
            };
        }

        return (modifires, key);
    }
}
