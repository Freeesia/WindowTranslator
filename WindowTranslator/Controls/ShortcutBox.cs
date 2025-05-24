using System.Windows;
using System.Windows.Input;
using WindowTranslator.Extensions;
using Wpf.Ui.Controls;

namespace WindowTranslator.Controls;

/// <summary>
/// ショートカット入力を受け付けるテキストボックス
/// </summary>
public class ShortcutBox : TextBox
{
    static ShortcutBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ShortcutBox), new FrameworkPropertyMetadata(typeof(ShortcutBox)));
        ClearButtonEnabledProperty.OverrideMetadata(typeof(ShortcutBox), new FrameworkPropertyMetadata(true));
        ShowClearButtonProperty.OverrideMetadata(typeof(ShortcutBox), new FrameworkPropertyMetadata(true));
        ContextMenuProperty.OverrideMetadata(typeof(ShortcutBox), new FrameworkPropertyMetadata(null));
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        SetCurrentValue(TextProperty, (e.KeyboardDevice.Modifiers, e.Key).ToShortcutString());
        e.Handled = true;
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClearButtonClick()
    {
        SetCurrentValue(TextProperty, (ModifierKeys.Control | ModifierKeys.Alt, Key.O).ToShortcutString());
    }
}
