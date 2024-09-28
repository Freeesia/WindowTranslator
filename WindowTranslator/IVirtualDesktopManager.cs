using System.Runtime.InteropServices;

namespace WindowTranslator;

[InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
public interface IVirtualDesktopManager
{
    bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
    Guid GetWindowDesktopId(IntPtr hWnd);
    int MoveWindowToDesktop(IntPtr hWnd, ref Guid desktop);
}
