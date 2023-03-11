using System;
using System.Runtime.InteropServices;

namespace HwndExtensions.Utils
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    };

    internal static class Win32
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hwnd, uint wCmd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr ChildWindowFromPoint(IntPtr hwndParent, POINT point);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ScreenToClient(IntPtr hwnd, ref POINT point);

        public static IntPtr DescendantWindowFromPoint(IntPtr hwndParent, POINT absPoint, out POINT relativePoint)
        {
            while (true)
            {
                relativePoint = absPoint;
                ScreenToClient(hwndParent, ref relativePoint);

                IntPtr child = ChildWindowFromPoint(hwndParent, relativePoint);
                if (child == IntPtr.Zero || child == hwndParent) return child;
                hwndParent = child;
            }
        }

        public static IntPtr DescendantWindowFromCursor(IntPtr hwndParent, out POINT point)
        {
            POINT cursor;
            if (!GetCursorPos(out cursor))
            {
                point = new POINT();
                return IntPtr.Zero;
            }

            return DescendantWindowFromPoint(hwndParent, cursor, out point);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindowAsync(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public const int
            GWL_STYLE = -16,
            VK_SHIFT = 0x10,
            VK_CONTROL = 0x11,
            VK_MENU = 0x12,
            WH_CALLWNDPROCRET = 12,
            WH_KEYBOARD_LL = 13,
            WH_MOUSE = 7,
            WM_MOUSEACTIVATE = 0x0021,
            WM_ACTIVATE = 0x06,
            WM_WINDOWPOSCHANGED = 0x0047,
            WM_GETMINMAXINFO = 0x0024;

        public const uint
            WS_CHILD = 0x40000000,
            WS_CLIPCHILDREN = 0x02000000,

            SWP_FRAMECHANGED = 0x0020,
            SWP_NOSIZE = 0x0001,
            SWP_NOMOVE = 0x0002,
            SWP_NOZORDER = 0x0004,
            SWP_NOACTIVATE = 0x10,
            SWP_ASYNCWINDOWPOS = 16384,
            SWP_HIDEWINDOW = 0x0080,
            SWP_SHOWWINDOW = 0x0040,
            SWP_NOREPOSITION = 0x0200,
            SWP_NOOWNERZORDER = 0x0200,

            WM_KEYFIRST = 0x0100,
            WM_KEYDOWN = 0x0100,
            WM_KEYUP = 0x0101,
            WM_CHAR = 0x0102,
            WM_DEADCHAR = 0x0103,
            WM_SYSKEYDOWN = 0x0104,
            WM_SYSKEYUP = 0x0105,
            WM_SYSCHAR = 0x0106,
            WM_SYSDEADCHAR = 0x0107,
            WM_KEYLAST = 0x0108,
            WM_IME_STARTCOMPOSITION = 0x010D,
            WM_IME_ENDCOMPOSITION = 0x010E,
            WM_IME_COMPOSITION = 0x010F,
            WM_IME_KEYLAST = 0x010F,
            WS_OVERLAPPED = 0x00000000,
            GW_HWNDNEXT = 2,
            GW_HWNDPREV = 3,
            GW_OWNER = 4;

        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int WHEEL_DELTA = 120,
                         WS_POPUP = unchecked((int)0x80000000),
                         WS_MINIMIZE = 0x20000000,
                         WS_VISIBLE = 0x10000000,
                         WS_DISABLED = 0x08000000,
                         WS_CLIPSIBLINGS = 0x04000000,
                         WS_MAXIMIZE = 0x01000000,
                         WS_CAPTION = 0x00C00000,
                         WS_BORDER = 0x00800000,
                         WS_DLGFRAME = 0x00400000,
                         WS_VSCROLL = 0x00200000,
                         WS_HSCROLL = 0x00100000,
                         WS_SYSMENU = 0x00080000,
                         WS_THICKFRAME = 0x00040000,
                         WS_TABSTOP = 0x00010000,
                         WS_MINIMIZEBOX = 0x00020000,
                         WS_MAXIMIZEBOX = 0x00010000,
                         WS_EX_DLGMODALFRAME = 0x00000001,
                         WS_EX_TRANSPARENT = 0x00000020,
                         WS_EX_MDICHILD = 0x00000040,
                         WS_EX_TOOLWINDOW = 0x00000080,
                         WS_EX_WINDOWEDGE = 0x00000100,
                         WS_EX_CLIENTEDGE = 0x00000200,
                         WS_EX_CONTEXTHELP = 0x00000400,
                         WS_EX_RIGHT = 0x00001000,
                         WS_EX_LEFT = 0x00000000,
                         WS_EX_RTLREADING = 0x00002000,
                         WS_EX_LEFTSCROLLBAR = 0x00004000,
                         WS_EX_CONTROLPARENT = 0x00010000,
                         WS_EX_STATICEDGE = 0x00020000,
                         WS_EX_APPWINDOW = 0x00040000,
                         WS_EX_LAYERED = 0x00080000,
                         WS_EX_TOPMOST = 0x00000008,
                         WS_EX_LAYOUTRTL = 0x00400000,
                         WS_EX_NOINHERITLAYOUT = 0x00100000,
                         WS_EX_COMPOSITED = 0x02000000,
                         WPF_SETMINPOSITION = 0x0001,
                         WPF_RESTORETOMAXIMIZED = 0x0002;

        public const int 
            SWP_NOREDRAW = 0x0008, 
            GWL_HWNDPARENT = -8, 
            SW_HIDE = 0, 
            SW_SHOWNA = 8,
            SW_SHOWNOACTIVATE = 0x0004;

        public static readonly IntPtr 
            HWND_BOTTOM = new IntPtr(1), 
            HWND_TOP = new IntPtr(0), 
            HWND_NOTOPMOST = new IntPtr(-2);
    }
}
