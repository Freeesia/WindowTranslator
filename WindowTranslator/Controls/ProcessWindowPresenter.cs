using HwndExtensions.Host;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32.Foundation;
using WindowTranslator.Stores;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Controls;
public class ProcessWindowPresenter : HwndHostPresenter
{
    public IProcessInfoStore? TargetProcess
    {
        get => (IProcessInfoStore?)GetValue(TargetProcessProperty);
        set => SetValue(TargetProcessProperty, value);
    }

    /// <summary>Identifies the <see cref="TargetProcess"/> dependency property.</summary>
    public static readonly DependencyProperty TargetProcessProperty =
        DependencyProperty.Register(
            nameof(TargetProcess),
            typeof(IProcessInfoStore),
            typeof(ProcessWindowPresenter),
            new PropertyMetadata(null, (d, e) => ((ProcessWindowPresenter)d).OnTargetProcessChanged((IProcessInfoStore?)e.NewValue)));

    private void OnTargetProcessChanged(IProcessInfoStore? newValue)
    {
        if (newValue is not null)
        {
            this.HwndHost = new ProcessWindowHost(newValue);
        }
    }

    private class ProcessWindowHost(IProcessInfoStore process) : HwndHost
    {
        private readonly IProcessInfoStore process = process;
        private nint beforeStyle;

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var childStyle = (nint)(WINDOW_STYLE.WS_CHILD |
                            // Child window should be have a thin-line border
                            WINDOW_STYLE.WS_BORDER |
                            // the parent cannot draw over the child's area. this is needed to avoid refresh issues
                            WINDOW_STYLE.WS_CLIPCHILDREN |
                            WINDOW_STYLE.WS_VISIBLE |
                            WINDOW_STYLE.WS_MAXIMIZE);
            this.beforeStyle = GetWindowLong((HWND)this.process.MainWindowHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            _ = SetWindowLongPtr((HWND)this.process.MainWindowHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, childStyle);
            SetParent((HWND)this.process.MainWindowHandle, (HWND)hwndParent.Handle);
            return new HandleRef(this, this.process.MainWindowHandle);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            SetParent((HWND)this.process.MainWindowHandle, HWND.Null);
            _ = SetWindowLongPtr((HWND)this.process.MainWindowHandle, WINDOW_LONG_PTR_INDEX.GWL_STYLE, this.beforeStyle);
            DestroyWindow((HWND)hwnd.Handle);
        }
    }
}
