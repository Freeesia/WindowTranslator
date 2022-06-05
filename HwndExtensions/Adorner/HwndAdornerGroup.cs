using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using HwndExtensions.Utils;

namespace HwndExtensions.Adorner
{
    /// <summary>
    /// An internal class for managing the connection of a group of HwndAdorner's to their owner window.
    /// The HwndAdorner searches up the visual tree for an IHwndAdornerManager containing an instance of this group,
    /// if an IHwndAdornerManager is not found it creates a group containing only itself
    /// </summary>
    internal class HwndAdornerGroup : HwndSourceConnector
    {
        // This class manages its base class resources (HwndSourceConnector) on its own.
        // i.e. when appropriately used, it dos not need to be disposed.

        #region Fields

        private readonly List<HwndAdorner> m_adornersInGroup;

        private HwndSource m_ownerSource;
        private bool m_owned;

        #endregion

        #region Win32 constants

        private const uint SET_ONLY_ZORDER = Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE;

        #endregion

        #region CTOR

        internal HwndAdornerGroup(UIElement commonAncestor) : base(commonAncestor)
        {
            m_adornersInGroup = new List<HwndAdorner>();
        }

        #endregion

        #region internal API

        internal bool Owned
        {
            get { return m_owned; }
        }

        private bool HasAdorners
        {
            get { return m_adornersInGroup.Count > 0; }
        }

        internal bool AddAdorner(HwndAdorner adorner)
        {
            if (!Activated)
            {
                Activate();
            }

            if (!m_adornersInGroup.Contains(adorner))
            {
                m_adornersInGroup.Add(adorner);
            }

            if (m_owned)
            {
                SetOwnership(adorner);
                ActivateInGroupLimits(adorner);
                adorner.InvalidateAppearance();

                var root = (UIElement) m_ownerSource.RootVisual;
                adorner.UpdateOwnerPosition(GetRectFromRoot(root));
            }

            return true;
        }

        private static Rect GetRectFromRoot(UIElement root)
        {
            return new Rect(
                root.PointToScreen(new Point()), 
                root.PointToScreen(new Point(root.RenderSize.Width, root.RenderSize.Height))
                );
        }

        internal bool RemoveAdorner(HwndAdorner adorner)
        {
            var res = m_adornersInGroup.Remove(adorner);

            if (m_owned)
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

        #endregion

        #region overrides

        protected override void OnSourceConnected(HwndSource connectedSource)
        {
            if(m_owned) DisconnectFromOwner();

            m_ownerSource = connectedSource;
            m_ownerSource.AddHook(OwnerHook);
            m_owned = true;

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

        #endregion

        #region private Methods

        private void DisconnectFromOwner()
        {
            if (m_owned)
            {
                m_ownerSource.RemoveHook(OwnerHook);
                m_ownerSource = null;
                m_owned = false;

                RemoveOwnership();
                InvalidateAppearance();
            }
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
        {
            Win32.SetWindowLong(adorner.Handle, Win32.GWL_HWNDPARENT, m_ownerSource.Handle);
        }

        private void RemoveOwnership()
        {
            foreach (var adorner in m_adornersInGroup)
            {
                RemoveOwnership(adorner);
            }
        }

        private static void RemoveOwnership(HwndAdorner adorner)
        {
            Win32.SetWindowLong(adorner.Handle, Win32.GWL_HWNDPARENT, IntPtr.Zero);
        }

        private void SetPosition()
        {
            var root = m_ownerSource.RootVisual as UIElement;
            if(root == null) return;

            var rect = GetRectFromRoot(root);
            foreach (var adorner in m_adornersInGroup)
            {
                adorner.UpdateOwnerPosition(rect);
            }
        }

        private void SetZOrder()
        {
            // getting the hwnd above the owner (in win32, the prev hwnd is the one visually above)
            var hwndAbove = Win32.GetWindow(m_ownerSource.Handle, Win32.GW_HWNDPREV);

            if (hwndAbove == IntPtr.Zero && HasAdorners)
                // owner is the Top most window
            {
                // randomly selecting an owned hwnd
                var owned = m_adornersInGroup.First().Handle;
                // setting owner after (visually under) it 
                Win32.SetWindowPos(m_ownerSource.Handle, owned, 0, 0, 0, 0, SET_ONLY_ZORDER);

                // now this is the 'above' hwnd
                hwndAbove = owned;
            }

            // inserting all adorners between the owner and the hwnd initially above it
            // currently not preserving any previous z-order state between the adorners (unsupported for now)
            foreach (var adorner in m_adornersInGroup)
            {
                var handle = adorner.Handle;
                Win32.SetWindowPos(handle, hwndAbove, 0, 0, 0, 0, SET_ONLY_ZORDER);
                hwndAbove = handle;
            }
        }

        internal void ActivateInGroupLimits(HwndAdorner adorner)
        {
            if( !m_owned ) return;

            var current = m_ownerSource.Handle;

            // getting the hwnd above the owner (in win32, the prev hwnd is the one visually above)
            var prev = Win32.GetWindow(current, Win32.GW_HWNDPREV);

            // searching up for the first non-sibling hwnd
            while (IsSibling(prev))
            {
                current = prev;
                prev = Win32.GetWindow(current, Win32.GW_HWNDPREV);
            }

            if (prev == IntPtr.Zero)
                // the owner or one of the siblings is the Top-most window
            {
                // setting the Top-most under the activated adorner
                Win32.SetWindowPos(current, adorner.Handle, 0, 0, 0, 0, SET_ONLY_ZORDER);
            }

            else
            {
                // setting the activated adorner under the first non-sibling hwnd
                Win32.SetWindowPos(adorner.Handle, prev, 0, 0, 0, 0, SET_ONLY_ZORDER);
            }
        }

        private bool IsSibling(IntPtr hwnd)
        {
            return m_adornersInGroup.Exists(o => o.Handle == hwnd);
        }

        private IntPtr OwnerHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32.WM_WINDOWPOSCHANGED) 
            {
                SetPosition();
            }

            return IntPtr.Zero;
        }

        #endregion
    }
}
