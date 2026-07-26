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
        // CsWin32 cannot generate the architecture-specific SetWindowLongPtrW for AnyCPU.
        if (nuint.Size == 4)
        {
            return SetWindowLong(hWnd, nIndex, unchecked((int)dwNewLong));
        }

        return SetWindowLongPtrW.Value(hWnd, (int)nIndex, dwNewLong);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi, SetLastError = true)]
    private delegate nint SetWindowLongPtrWDelegate(nint hWnd, int nIndex, nint dwNewLong);

    private static class SetWindowLongPtrW
    {
        private static readonly nint library = NativeLibrary.Load(
            "USER32.dll",
            typeof(PInvoke).Assembly,
            DllImportSearchPath.System32);

        internal static readonly SetWindowLongPtrWDelegate Value =
            Marshal.GetDelegateForFunctionPointer<SetWindowLongPtrWDelegate>(
                NativeLibrary.GetExport(library, nameof(SetWindowLongPtrW)));
    }
}
