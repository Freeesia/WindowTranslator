using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WindowTranslator.Plugin.BergamotTranslatorPlugin;

public static partial class SystemUtility
{
    public static string ModelsPath { get; } = Path.Combine(PathUtility.SharedDir, "bergamot");

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

    private static void MigrateModelsIfNeeded()
    {
        var oldPath = Path.Combine(PathUtility.UserDir, "bergamot");
        if (Directory.Exists(oldPath) && !Directory.Exists(ModelsPath))
        {
            Directory.CreateDirectory(PathUtility.SharedDir);
            Directory.Move(oldPath, ModelsPath);
        }
    }

    [ModuleInitializer]
    internal static void Initialize()
    {
        MigrateModelsIfNeeded();
    }
}
