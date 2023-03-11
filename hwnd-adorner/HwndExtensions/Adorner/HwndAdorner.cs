﻿using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using HwndExtensions.Utils;
using PInvoke;

namespace HwndExtensions.Adorner;

/// <summary>
/// A class for managing an adornment above all other content (including non-WPF child windows (hwnd), unlike the WPF Adorner classes)
/// </summary>
public sealed class HwndAdorner : IDisposable
{
    // See the HwndAdornerElement class for a simple usage example.
    // 
    // Another way of using this class is through the HwndExtensions.HwndAdornment attached property,
    // which can attach any UIElement as an Adornment to any FrameworkElement. 
    // This option lacks the logical parenting provided by HwndAdornerElement. 
    // 
    // Event routing should work in any case (through the GetUIParentCore override of the HwndAdornmentRoot class)

    private readonly FrameworkElement m_elementAttachedTo;
    private readonly HwndAdornmentRoot m_hwndAdornmentRoot;
    private UIElement? m_adornment;
    private HwndAdornerGroup? m_hwndAdornerGroup;
    private HwndSource? m_hwndSource;
    private bool m_shown;

    private Rect m_parentBoundingBox;
    private Rect m_boundingBox;

    private bool m_disposed;

    private const User32.SetWindowPosFlags NO_REPOSITION_FLAGS = User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOACTIVATE |
                                             User32.SetWindowPosFlags.SWP_NOZORDER | User32.SetWindowPosFlags.SWP_NOOWNERZORDER | User32.SetWindowPosFlags.SWP_NOREPOSITION;

    private const User32.SetWindowPosFlags SET_ONLY_LOCATION = User32.SetWindowPosFlags.SWP_NOACTIVATE | User32.SetWindowPosFlags.SWP_NOZORDER | User32.SetWindowPosFlags.SWP_NOOWNERZORDER;

    public HwndAdorner(FrameworkElement attachedTo)
    {
        m_elementAttachedTo = attachedTo;
        m_parentBoundingBox = m_boundingBox = new Rect(new Point(), new Size());

        m_hwndAdornmentRoot = new HwndAdornmentRoot()
        {
            UIParentCore = m_elementAttachedTo
        };

        m_elementAttachedTo.Loaded += OnLoaded;
        m_elementAttachedTo.Unloaded += OnUnloaded;
        m_elementAttachedTo.IsVisibleChanged += OnIsVisibleChanged;
        m_elementAttachedTo.LayoutUpdated += OnLayoutUpdated;
    }

    internal IntPtr Handle => m_hwndSource?.Handle ?? IntPtr.Zero;

    internal void InvalidateAppearance()
    {
        if (m_hwndSource == null) return;

        if (NeedsToAppear)
        {
            if (!m_shown)
            {
                User32.SetWindowPos(m_hwndSource.Handle, IntPtr.Zero, 0, 0, 0, 0, NO_REPOSITION_FLAGS | User32.SetWindowPosFlags.SWP_SHOWWINDOW);
                m_shown = true;
            }
        }
        else
        {
            if (m_shown)
            {
                User32.SetWindowPos(m_hwndSource.Handle, IntPtr.Zero, 0, 0, 0, 0, NO_REPOSITION_FLAGS | User32.SetWindowPosFlags.SWP_HIDEWINDOW);
                m_shown = false;
            }
        }
    }

    internal void UpdateOwnerPosition(Rect rect)
    {
        if (!m_parentBoundingBox.Equals(rect))
        {
            m_parentBoundingBox = rect;
            SetAbsolutePosition();
        }
    }


    public FrameworkElement Root => m_hwndAdornmentRoot;

    public UIElement? Adornment
    {
        get => m_adornment;
        set
        {
            if (m_disposed)
                throw new ObjectDisposedException("HwndAdorner");

            m_adornment = value;
            if (m_elementAttachedTo.IsLoaded)
                m_hwndAdornmentRoot.SetValue(ContentControl.ContentProperty, m_adornment);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        InitHwndSource();
        m_hwndAdornmentRoot.SetCurrentValue(ContentControl.ContentProperty, m_adornment);
        ConnectToGroup();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        DisconnectFromGroup();
        m_hwndAdornmentRoot.SetCurrentValue(ContentControl.ContentProperty, null);
        DisposeHwndSource();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        InvalidateAppearance();
    }

    private void OnLayoutUpdated(object? sender, EventArgs eventArgs)
    {
        var source = PresentationSource.FromVisual(m_elementAttachedTo);
        var ct = source?.CompositionTarget;

        if (ct?.RootVisual != null)
        {
            UpdateBoundingBox(CalculateAssignedRC(source!));
        }
    }

    private void UpdateBoundingBox(Rect boundingBox)
    {
        if (!m_boundingBox.Equals(boundingBox))
        {
            m_boundingBox = boundingBox;
            SetAbsolutePosition();
        }
    }

    private Rect CalculateAssignedRC(PresentationSource source)
    {
        Rect rectElement = new Rect(m_elementAttachedTo.RenderSize);
        Rect rectRoot = RectUtil.ElementToRoot(rectElement, m_elementAttachedTo, source);
        return RectUtil.RootToClient(rectRoot, source);
    }

    private bool Owned => m_hwndAdornerGroup?.Owned ?? false;

    private bool NeedsToAppear => Owned && m_elementAttachedTo.IsVisible;

    private void ConnectToGroup()
    {
        DisconnectFromGroup();

        var manager = m_elementAttachedTo.TryFindVisualAncestor<IHwndAdornerManager>();
        m_hwndAdornerGroup = manager?.AdornerGroup ?? new HwndAdornerGroup(m_elementAttachedTo);
        m_hwndAdornerGroup.AddAdorner(this);
    }

    private void DisconnectFromGroup()
    {
        if (m_hwndAdornerGroup == null) return;

        m_hwndAdornerGroup.RemoveAdorner(this);
        m_hwndAdornerGroup = null;
    }

    private void SetAbsolutePosition()
    {
        if (m_hwndSource == null) return;

        User32.SetWindowPos(m_hwndSource.Handle, IntPtr.Zero,
            (int)(m_parentBoundingBox.X + m_boundingBox.X),
            (int)(m_parentBoundingBox.Y + m_boundingBox.Y),
            (int)(Math.Min(m_boundingBox.Width, m_parentBoundingBox.Width - m_boundingBox.X)),
            (int)(Math.Min(m_boundingBox.Height, m_parentBoundingBox.Height - m_boundingBox.Y)),
            SET_ONLY_LOCATION | User32.SetWindowPosFlags.SWP_ASYNCWINDOWPOS);
    }

    private void InitHwndSource()
    {
        if (m_hwndSource != null) return;

        int classStyle = 0;
        int style = 0;
        int styleEx = (int)User32.WindowStylesEx.WS_EX_NOACTIVATE;

        var parameters = new HwndSourceParameters()
        {
            UsesPerPixelOpacity = true,
            WindowClassStyle = classStyle,
            WindowStyle = style,
            ExtendedWindowStyle = styleEx,
            PositionX = (int)(m_parentBoundingBox.X + m_boundingBox.X),
            PositionY = (int)(m_parentBoundingBox.Y + m_boundingBox.Y),
            Width = (int)(m_boundingBox.Width),
            Height = (int)(m_boundingBox.Height)
        };

        m_hwndSource = new HwndSource(parameters);
        m_hwndSource.RootVisual = m_hwndAdornmentRoot;
        m_hwndSource.AddHook(WndProc);

        m_shown = false;
    }

    private void DisposeHwndSource()
    {
        if (m_hwndSource == null) return;

        m_hwndSource.RemoveHook(WndProc);
        m_hwndSource.Dispose();
        m_hwndSource = null;

        m_shown = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var m = (User32.WindowMessage)msg;
        if (m == User32.WindowMessage.WM_ACTIVATE)
        {
            m_hwndAdornerGroup?.ActivateInGroupLimits(this);
        }
        else if (m == User32.WindowMessage.WM_GETMINMAXINFO)
        {
            var minMaxInfo = Marshal.PtrToStructure<User32.MINMAXINFO>(lParam);
            minMaxInfo.ptMinTrackSize = new POINT();
            Marshal.StructureToPtr(minMaxInfo, lParam, true);
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (m_disposed) return;

        DisconnectFromGroup();
        m_hwndAdornmentRoot.SetCurrentValue(ContentControl.ContentProperty, null);
        DisposeHwndSource();

        m_elementAttachedTo.Loaded -= OnLoaded;
        m_elementAttachedTo.Unloaded -= OnUnloaded;
        m_elementAttachedTo.IsVisibleChanged -= OnIsVisibleChanged;
        m_elementAttachedTo.LayoutUpdated -= OnLayoutUpdated;

        m_disposed = true;
    }
}
