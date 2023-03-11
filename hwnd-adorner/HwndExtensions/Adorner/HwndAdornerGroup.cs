using System.Windows;
using System.Windows.Interop;
using HwndExtensions.Utils;
using PInvoke;

namespace HwndExtensions.Adorner;

/// <summary>
/// An internal class for managing the connection of a group of HwndAdorner's to their owner window.
/// The HwndAdorner searches up the visual tree for an IHwndAdornerManager containing an instance of this group,
/// if an IHwndAdornerManager is not found it creates a group containing only itself
/// </summary>
internal class HwndAdornerGroup : HwndSourceConnector
{
    // This class manages its base class resources (HwndSourceConnector) on its own.
    // i.e. when appropriately used, it dos not need to be disposed.

    private readonly HashSet<HwndAdorner> m_adornersInGroup = new();

    private HwndSource? m_ownerSource;

    private const User32.SetWindowPosFlags SET_ONLY_ZORDER = User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOACTIVATE;

    internal HwndAdornerGroup(UIElement commonAncestor)
        : base(commonAncestor)
    {
    }

    internal bool Owned => m_ownerSource is not null;
    private bool HasAdorners => m_adornersInGroup.Count > 0;

    internal bool AddAdorner(HwndAdorner adorner)
    {
        if (!Activated)
        {
            Activate();
        }

        m_adornersInGroup.Add(adorner);

        if (m_ownerSource is not null)
        {
            SetOwnership(adorner);
            ActivateInGroupLimits(adorner);
            adorner.InvalidateAppearance();

            var root = (UIElement)m_ownerSource.RootVisual;
            adorner.UpdateOwnerPosition(GetRectFromRoot(root));
        }

        return true;
    }

    private static Rect GetRectFromRoot(UIElement root)
        => new(root.PointToScreen(new Point()), root.PointToScreen(new Point(root.RenderSize.Width, root.RenderSize.Height)));

    internal bool RemoveAdorner(HwndAdorner adorner)
    {
        var res = m_adornersInGroup.Remove(adorner);

        if (Owned)
        {
            RemoveOwnership(adorner);
            adorner.InvalidateAppearance();
        }

        if (!HasAdorners)
        {
            Deactivate();
        }

        return res;
    }

    protected override void OnSourceConnected(HwndSource connectedSource)
    {
        if (Owned) DisconnectFromOwner();

        m_ownerSource = connectedSource;
        m_ownerSource.AddHook(OwnerHook);

        if (HasAdorners)
        {
            SetOwnership();
            SetZOrder();
            SetPosition();
            InvalidateAppearance();
        }
    }

    protected override void OnSourceDisconnected(HwndSource disconnectedSource)
    {
        DisconnectFromOwner();
    }

    private void DisconnectFromOwner()
    {
        if (m_ownerSource is null) return;

        m_ownerSource.RemoveHook(OwnerHook);
        m_ownerSource = null;

        RemoveOwnership();
        InvalidateAppearance();
    }

    private void SetOwnership()
    {
        foreach (var adorner in m_adornersInGroup)
        {
            SetOwnership(adorner);
        }
    }

    private void InvalidateAppearance()
    {
        foreach (var adorner in m_adornersInGroup)
        {
            adorner.InvalidateAppearance();
        }
    }

    private void SetOwnership(HwndAdorner adorner)
        => User32.SetWindowLongPtr(adorner.Handle, User32.WindowLongIndexFlags.GWLP_HWNDPARENT, m_ownerSource?.Handle ?? throw new InvalidOperationException());

    private void RemoveOwnership()
    {
        foreach (var adorner in m_adornersInGroup)
        {
            RemoveOwnership(adorner);
        }
    }

    private static void RemoveOwnership(HwndAdorner adorner)
        => User32.SetWindowLongPtr(adorner.Handle, User32.WindowLongIndexFlags.GWLP_HWNDPARENT, IntPtr.Zero);

    private void SetPosition()
    {
        if (m_ownerSource?.RootVisual is not UIElement root) return;

        var rect = GetRectFromRoot(root);
        foreach (var adorner in m_adornersInGroup)
        {
            adorner.UpdateOwnerPosition(rect);
        }
    }

    private void SetZOrder()
    {
        if (m_ownerSource is null) return;

        // getting the hwnd above the owner (in win32, the prev hwnd is the one visually above)
        var hwndAbove = User32.GetWindow(m_ownerSource.Handle, User32.GetWindowCommands.GW_HWNDPREV);

        if (hwndAbove == IntPtr.Zero && HasAdorners)
        // owner is the Top most window
        {
            // randomly selecting an owned hwnd
            var owned = m_adornersInGroup.First().Handle;
            // setting owner after (visually under) it 
            User32.SetWindowPos(m_ownerSource.Handle, owned, 0, 0, 0, 0, SET_ONLY_ZORDER);

            // now this is the 'above' hwnd
            hwndAbove = owned;
        }

        // inserting all adorners between the owner and the hwnd initially above it
        // currently not preserving any previous z-order state between the adorners (unsupported for now)
        foreach (var adorner in m_adornersInGroup)
        {
            var handle = adorner.Handle;
            User32.SetWindowPos(handle, hwndAbove, 0, 0, 0, 0, SET_ONLY_ZORDER);
            hwndAbove = handle;
        }
    }

    internal void ActivateInGroupLimits(HwndAdorner adorner)
    {
        if (m_ownerSource is null) return;

        var current = m_ownerSource.Handle;

        // getting the hwnd above the owner (in win32, the prev hwnd is the one visually above)
        var prev = User32.GetWindow(current, User32.GetWindowCommands.GW_HWNDPREV);

        // searching up for the first non-sibling hwnd
        while (m_adornersInGroup.Any(o => o.Handle == prev))
        {
            current = prev;
            prev = User32.GetWindow(current, User32.GetWindowCommands.GW_HWNDPREV);
        }

        if (prev == IntPtr.Zero)
        // the owner or one of the siblings is the Top-most window
        {
            // setting the Top-most under the activated adorner
            User32.SetWindowPos(current, adorner.Handle, 0, 0, 0, 0, SET_ONLY_ZORDER);
        }
        else
        {
            // setting the activated adorner under the first non-sibling hwnd
            User32.SetWindowPos(adorner.Handle, prev, 0, 0, 0, 0, SET_ONLY_ZORDER);
        }
    }

    private IntPtr OwnerHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)User32.WindowMessage.WM_WINDOWPOSCHANGED)
        {
            SetPosition();
        }

        return IntPtr.Zero;
    }
}
