using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Windows.Win32;

internal static partial class PInvoke
{
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