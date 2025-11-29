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
}