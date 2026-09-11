using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowTranslator.Extensions;
using static Windows.Win32.PInvoke;

namespace WindowTranslator;

internal static class SingleInstanceWindowActivator
{
    private const string ActivationMessageName = "WindowTranslator.ActivateStartupDialog";
    internal const string StartupDialogMarker = "WindowTranslator.StartupDialog";
    internal static uint ActivationMessage { get; } = RegisterActivationMessage();
    private static readonly SafeFileHandle MarkerValue = new(new(1), ownsHandle: false);

    public static void Register(Window window, Action activate)
    {
        HWND handle = HWND.Null;
        HwndSource? source = null;
        window.SourceInitialized += (_, _) =>
        {
            handle = new(new WindowInteropHelper(window).Handle);
            if (!SetProp(handle, StartupDialogMarker, MarkerValue))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            source = HwndSource.FromHwnd(handle);
            source.AddHook(WndProc);
        };
        window.Closed += (_, _) =>
        {
            source?.RemoveHook(WndProc);
            if (!handle.IsNull)
            {
                using SafeFileHandle marker = RemoveProp(handle, StartupDialogMarker);
                marker.SetHandleAsInvalid();
            }
        };

        IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (unchecked((uint)msg) == ActivationMessage)
            {
                activate();
                handled = true;
            }
            return IntPtr.Zero;
        }
    }

    public static bool TryActivateExistingInstance()
    {
        HashSet<int> processIds = GetExistingInstanceProcessIds();
        if (processIds.Count == 0)
        {
            return false;
        }

        HWND startupDialog = HWND.Null;
        int startupProcessId = 0;
        EnumWindows((hWnd, _) =>
        {
            if (!hWnd.TryGetProcessId(out int processId) || !processIds.Contains(processId))
            {
                return true;
            }

            if (HasStartupDialogMarker(hWnd))
            {
                startupDialog = hWnd;
                startupProcessId = processId;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (startupDialog.IsNull)
        {
            return false;
        }

        _ = AllowSetForegroundWindow(unchecked((uint)startupProcessId));
        return PostMessage(startupDialog, ActivationMessage, default, default);
    }

    private static uint RegisterActivationMessage()
    {
        uint message = RegisterWindowMessage(ActivationMessageName);
        return message != 0 ? message : throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static bool HasStartupDialogMarker(HWND hWnd)
    {
        using SafeFileHandle marker = GetProp(hWnd, StartupDialogMarker);
        bool hasMarker = !marker.IsInvalid;
        marker.SetHandleAsInvalid();
        return hasMarker;
    }

    private static HashSet<int> GetExistingInstanceProcessIds()
    {
        using Process currentProcess = Process.GetCurrentProcess();
        HashSet<int> processIds = [];
        foreach (Process process in Process.GetProcessesByName(currentProcess.ProcessName))
        {
            using (process)
            {
                if (process.Id != Environment.ProcessId && IsSameExecutable(process))
                {
                    processIds.Add(process.Id);
                }
            }
        }
        return processIds;
    }

    private static bool IsSameExecutable(Process process)
    {
        if (Environment.ProcessPath is not { } currentPath)
        {
            return true;
        }

        try
        {
            return string.Equals(process.MainModule?.FileName, currentPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Win32Exception)
        {
            // 同名プロセスの実行ファイルを参照できない環境でも、mutexの所有プロセスを候補から外さない。
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
