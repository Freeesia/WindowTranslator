using System.Runtime.Versioning;

namespace Windows.Win32;
internal static partial class PInvoke
{
    [SupportedOSPlatform("windows5.0")]
    internal static unsafe uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId)
    {
        uint id = 0;
        var bRef = GetWindowThreadProcessId(new(hWnd), &id);
        lpdwProcessId = unchecked((int)id);
        return bRef;

    }
}