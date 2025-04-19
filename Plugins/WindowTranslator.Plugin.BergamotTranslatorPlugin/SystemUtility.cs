using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowTranslator.Plugin.BergamotTranslatorPlugin;

public static partial class SystemUtility
{
    private const ushort IMAGE_FILE_MACHINE_AMD64 = 0x8664;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWow64Process2(nint hProcess, out ushort processMachine, out ushort nativeMachine);

    public static bool IsX64Machine()
    {
        if (IsWow64Process2(Process.GetCurrentProcess().Handle, out _, out var nativeMachine))
        {
            return nativeMachine == IMAGE_FILE_MACHINE_AMD64;
        }
        return false;
    }
}
