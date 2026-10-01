using System.Runtime.InteropServices;
using Windows.Win32.Graphics.Gdi;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Extensions;

internal enum CaptureTargetKind
{
    Window,
    Monitor,
}

internal static class CaptureTargetHandleExtensions
{
    public static CaptureTargetKind GetCaptureTargetKind(this IntPtr targetHandle)
    {
        if (IsWindow(new(targetHandle)))
        {
            return CaptureTargetKind.Window;
        }

        var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (GetMonitorInfo(new(targetHandle), ref monitorInfo))
        {
            return CaptureTargetKind.Monitor;
        }

        throw new ArgumentException("Target handle is not a valid window or monitor.", nameof(targetHandle));
    }
}
