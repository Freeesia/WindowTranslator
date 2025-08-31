using Wpf.Ui;
using Wpf.Ui.Controls;

namespace WindowTranslator.Extensions;
public static class WpfUiExtensions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    public static void ShowError(this ISnackbarService snackbar, string title, string message, SymbolRegular? icon = null, TimeSpan? timeout = null)
        => snackbar.Show(title, message, ControlAppearance.Danger, new SymbolIcon(icon ?? SymbolRegular.DismissCircle24, filled: true), timeout ?? DefaultTimeout);

    public static void ShowWarning(this ISnackbarService snackbar, string title, string message, SymbolRegular? icon = null, TimeSpan? timeout = null)
        => snackbar.Show(title, message, ControlAppearance.Caution, new SymbolIcon(icon ?? SymbolRegular.Warning24, filled: true), timeout ?? DefaultTimeout);

    public static void ShowSuccess(this ISnackbarService snackbar, string title, string message, SymbolRegular? icon = null, TimeSpan? timeout = null)
        => snackbar.Show(title, message, ControlAppearance.Success, new SymbolIcon(icon ?? SymbolRegular.CheckmarkCircle24, filled: true), timeout ?? DefaultTimeout);

    public static void ShowInfo(this ISnackbarService snackbar, string title, string message, SymbolRegular? icon = null, TimeSpan? timeout = null)
        => snackbar.Show(title, message, ControlAppearance.Info, new SymbolIcon(icon ?? SymbolRegular.Info24, filled: true), timeout ?? DefaultTimeout);
}
