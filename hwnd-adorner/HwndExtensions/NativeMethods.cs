using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Windows.Win32;

internal static partial class PInvoke
{
    [SupportedOSPlatform("windows5.0")]
    internal static nint SetWindowLongPtr(
        Foundation.HWND hWnd,
        UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex,
        nint dwNewLong)
    {
        if (nuint.Size == 4)
        {
            return SetWindowLong(hWnd, nIndex, unchecked((int)dwNewLong));
        }

        return SetWindowLongPtrW(hWnd, nIndex, dwNewLong);
    }

    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SupportedOSPlatform("windows5.0")]
    private static extern nint SetWindowLongPtrW(
        Foundation.HWND hWnd,
        UI.WindowsAndMessaging.WINDOW_LONG_PTR_INDEX nIndex,
        nint dwNewLong);
}
