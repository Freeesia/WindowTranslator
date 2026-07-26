using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Extensions;

internal static class WindowHandleExtensions
{
    public static string GetText(this HWND window)
    {
        var bufferSize = GetWindowTextLength(window);
        Span<char> buffer = stackalloc char[bufferSize + 1];
        var length = GetWindowText(window, buffer);
        return length == 0 ? string.Empty : new(buffer[..length]);
    }

    public static unsafe bool TryGetProcessId(this HWND window, out int processId)
    {
        uint id = 0;
        var threadId = GetWindowThreadProcessId(window, &id);
        processId = unchecked((int)id);
        return threadId != 0;
    }

    public static bool ShouldIgnore(this HWND window)
    {
        if (!IsWindowVisible(window) || window.IsCloaked())
        {
            return true;
        }

        Span<char> className = stackalloc char[256];
        var length = GetClassName(window, className);
        return className[..length] is "tooltips_class32" or "#32768";
    }

    public static unsafe (int Width, int Height) GetSizeForWgcCompare(this HWND window)
    {
        RECT rect;
        var result = DwmGetWindowAttribute(
            window,
            DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
            &rect,
            (uint)sizeof(RECT));
        if (result.Failed)
        {
            throw new InvalidOperationException("DwmGetWindowAttribute failed.");
        }

        return (rect.Width, rect.Height);
    }

    private static unsafe bool IsCloaked(this HWND window)
    {
        var cloaked = 0;
        var result = DwmGetWindowAttribute(
            window,
            DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
            &cloaked,
            sizeof(int));
        return result.Succeeded && cloaked != 0;
    }
}
