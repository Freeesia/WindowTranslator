using HwndExtensions.Host;
using PInvoke;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WindowTranslator.Stores;

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
        private IntPtr beforeStyle;

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var childStyle = (IntPtr)(User32.WindowStyles.WS_CHILD |
                                      // Child window should be have a thin-line border
                                      User32.WindowStyles.WS_BORDER |
                                      // the parent cannot draw over the child's area. this is needed to avoid refresh issues
                                      User32.WindowStyles.WS_CLIPCHILDREN |
                                      User32.WindowStyles.WS_VISIBLE |
                                      User32.WindowStyles.WS_MAXIMIZE);
            this.beforeStyle = User32.GetWindowLongPtr_IntPtr(this.process.MainWindowHandle, User32.WindowLongIndexFlags.GWL_STYLE);
            User32.SetWindowLongPtr(this.process.MainWindowHandle, User32.WindowLongIndexFlags.GWL_STYLE, childStyle);
            User32.SetParent(this.process.MainWindowHandle, hwndParent.Handle);
            return new HandleRef(this, this.process.MainWindowHandle);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            User32.SetParent(this.process.MainWindowHandle, IntPtr.Zero);
            User32.SetWindowLongPtr(this.process.MainWindowHandle, User32.WindowLongIndexFlags.GWL_STYLE, this.beforeStyle);
            User32.DestroyWindow(hwnd.Handle);
        }
    }
}
