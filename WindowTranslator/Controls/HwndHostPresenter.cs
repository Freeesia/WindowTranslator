using System.Collections;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static PInvoke.User32;

namespace WindowTranslator.Controls;

public class HwndHostPresenter : FrameworkElement
{
    private InternalHwndHost? child;

    public IntPtr WindowHandle
    {
        get => (IntPtr)GetValue(WindowHandleProperty);
        set => SetValue(WindowHandleProperty, value);
    }

    /// <summary>Identifies the <see cref="WindowHandle"/> dependency property.</summary>
    public static readonly DependencyProperty WindowHandleProperty =
        DependencyProperty.Register(nameof(WindowHandle), typeof(IntPtr), typeof(HwndHostPresenter), new PropertyMetadata(IntPtr.Zero, (d, e) => ((HwndHostPresenter)d).OnWindowHandleChanged()));

    public HwndHostPresenter()
    {
        Unloaded += (_, _) => child?.Dispose();
    }

    private void OnWindowHandleChanged()
    {
        RemoveVisualChild(child);
        child?.Dispose();
        if (WindowHandle == IntPtr.Zero)
        {
            child = null;
        }
        else
        {
            child = new InternalHwndHost(WindowHandle);
            AddVisualChild(child);
        }
        InvalidateMeasure();
    }

    protected override IEnumerator LogicalChildren
    {
        get
        {
            if (child is null)
            {
                yield break;
            }
            yield return child;
        }
    }

    protected override int VisualChildrenCount => child is null ? 0 : 1;

    protected override Visual GetVisualChild(int index) => child ?? throw new InvalidOperationException();

    protected override Size MeasureOverride(Size constraint)
    {
        if (child is not null)
        {
            child.Measure(constraint);
            return child.DesiredSize;
        }
        return Size.Empty;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (child is not null)
        {
            child.Arrange(new(arrangeSize));
        }
        return arrangeSize;
    }


    private class InternalHwndHost : HwndHost
    {
        private readonly IntPtr childHWnd;
        private readonly IntPtr hostingStyle = (IntPtr)
            // 親ウィンドウに埋め込む
            (WindowStyles.WS_CHILD |
            // 子ウィンドウの上に描画するために必要そう
            WindowStyles.WS_CLIPCHILDREN |
            WindowStyles.WS_VISIBLE |
            WindowStyles.WS_MAXIMIZE);
        private IntPtr originalStyle;

        public InternalHwndHost(IntPtr childHWnd) => this.childHWnd = childHWnd;

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var childRef = new HandleRef(this, childHWnd);
            originalStyle = (IntPtr)GetWindowLong(childHWnd, WindowLongIndexFlags.GWL_STYLE);
            SetWindowLongPtr(childHWnd, WindowLongIndexFlags.GWL_STYLE, hostingStyle);
            SetParent(childHWnd, hwndParent.Handle);
            return childRef;
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            SetParent(childHWnd, IntPtr.Zero);
            SetWindowLongPtr(childHWnd, WindowLongIndexFlags.GWL_STYLE, originalStyle);
        }
    }
}