using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Windows.Win32;
internal static partial class PInvoke
{
    [SupportedOSPlatform("windows5.0")]
    internal static unsafe string GetWindowText(Foundation.HWND hWnd)
    {
        var bufferSize = GetWindowTextLength(hWnd);
        Span<char> buffer = stackalloc char[bufferSize + 1];
        int length = GetWindowText(hWnd, buffer);
        if (length == 0)
        {
            return string.Empty;
        }
        return new string(buffer[..length]);
    }

    [SupportedOSPlatform("windows5.0")]
    internal static unsafe uint GetWindowThreadProcessId(Foundation.HWND hWnd, out int lpdwProcessId)
    {
        uint id = 0;
        var bRef = GetWindowThreadProcessId(hWnd, &id);
        lpdwProcessId = unchecked((int)id);
        return bRef;

    }

    [SupportedOSPlatform("windows5.0")]
    internal static int SetWindowLong(Foundation.HWND hWnd, UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex, UI.WindowsAndMessaging.WINDOW_EX_STYLE dwNewLong)
        => SetWindowLong(hWnd, nIndex, unchecked((int)dwNewLong));

    [SupportedOSPlatform("windows5.0")]
    internal static int SetWindowLongPtr(Foundation.HWND hWnd, UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong)
    {
        if (nuint.Size == 4)
        {
            return SetWindowLong(hWnd, nIndex, (int)dwNewLong);
        }
        return _SetWindowLongPtr(hWnd, nIndex, dwNewLong);
    }


    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "SetWindowLongPtrW", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SupportedOSPlatform("windows5.0")]
    private static extern int _SetWindowLongPtr(Foundation.HWND hWnd, UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong);

    /// <summary>
    /// ウィンドウがcloaked（非表示）状態かどうかを判定します。
    /// 注意: このメソッドは仮想デスクトップによる非表示を完全には検出できません。
    /// 仮想デスクトップの判定には IVirtualDesktopManager.IsWindowOnCurrentVirtualDesktop を併用してください。
    /// </summary>
    public static unsafe bool IsCloaked(Foundation.HWND hwnd)
    {
        var cloaked = 0;
        var hr = DwmGetWindowAttribute(hwnd, Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int));
        if (hr.Failed)
        {
            return false;
        }
        return cloaked != 0;
    }

    public static bool IsIgnoreWindow(Foundation.HWND hWnd)
    {
        // 非表示ウィンドウとcloakedウィンドウをスキップ
        if (!IsWindowVisible(hWnd) || IsCloaked(hWnd))
        {
            return true;
        }
        // ツールチップやコンテキストメニューは無視
        Span<char> className = stackalloc char[256];
        var l = GetClassName(hWnd, className);
        if (className[..l] is "tooltips_class32" or "#32768")
        {
            return true;
        }
        return false;
    }

    public static unsafe (int w, int h) GetWindowSizeForWgcCompare(Foundation.HWND hwnd)
    {
        // 1) 影を含まない枠サイズを取得（WGCのキャプチャ領域に近い）
        Foundation.RECT rect;
        if (DwmGetWindowAttribute(hwnd, Graphics.Dwm.DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &rect, (uint)Marshal.SizeOf<Foundation.RECT>()) != 0)
            throw new InvalidOperationException("DwmGetWindowAttribute failed.");

        return (rect.Width, rect.Height);
    }
}