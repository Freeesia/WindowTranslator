using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static PInvoke.User32;

namespace WindowTranslator;

public class HwndHostPresenter : FrameworkElement
{
    private InternalHwndHost? child;

    public IntPtr WindowHandle
    {
        get => (IntPtr)GetValue(WindowHandleProperty);
        set => SetValue(WindowHandleProperty, value);
    }

    /// <summary>Identifies the<see cref= "WindowHandle" /> dependency property.</summary>
    public static readonly DependencyProperty WindowHandleProperty =
        DependencyProperty.Register(nameof(WindowHandle), typeof(IntPtr), typeof(HwndHostPresenter), new PropertyMetadata(IntPtr.Zero, WindowHandleChanged));

    private static void WindowHandleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HwndHostPresenter)d).AttachWindow();

    public HwndHostPresenter()
    {
        this.Unloaded += (_, _) => this.child?.Dispose();
    }

    private void AttachWindow()
    {
        RemoveVisualChild(this.child);
        this.child?.Dispose();
        if (this.WindowHandle == IntPtr.Zero)
        {
            this.child = null;
        }
        else
        {
            this.child = new InternalHwndHost(this.WindowHandle);
            AddVisualChild(this.child);
        }
        InvalidateMeasure();
    }

    protected override IEnumerator LogicalChildren
    {
        get
        {
            if (this.child is null)
            {
                yield break;
            }
            yield return this.child;
        }
    }

    protected override int VisualChildrenCount => this.child is null ? 0 : 1;

    protected override Visual GetVisualChild(int index) => this.child ?? throw new InvalidOperationException();

    protected override Size MeasureOverride(Size constraint)
    {
        if (this.child is not null)
        {
            this.child.Measure(constraint);
            return this.child.DesiredSize;
        }
        return Size.Empty;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (this.child is not null)
        {
            this.child.Arrange(new(arrangeSize));
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
            this.originalStyle = (IntPtr)GetWindowLong(this.childHWnd, WindowLongIndexFlags.GWL_STYLE);
            SetWindowLongPtr(childHWnd, WindowLongIndexFlags.GWL_STYLE, this.hostingStyle);
            SetParent(childHWnd, hwndParent.Handle);
            return childRef;
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            SetParent(this.childHWnd, IntPtr.Zero);
            SetWindowLongPtr(this.childHWnd, WindowLongIndexFlags.GWL_STYLE, this.originalStyle);
        }
    }
}