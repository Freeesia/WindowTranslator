using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using HwndExtensions.Utils;

namespace HwndExtensions.Host
{
    /// <summary>
    /// A class for managing positions for a group of Hwnd's. 
    /// </summary>
    public class HwndHostGroup
    {
        private readonly FrameworkElement m_connector;
        private readonly List<IHwndHolder> m_hwndHolders;
        private readonly PointDistanceComparer m_comparer;
        private bool m_expandingAsync;

        public HwndHostGroup(FrameworkElement connector)
        {
            m_connector = connector;
            m_hwndHolders = new List<IHwndHolder>();
            m_comparer = new PointDistanceComparer();

            connector.Loaded += OnConnectorLoaded;
            connector.Unloaded += OnConnectorUnloaded;
        }

        private void OnConnectorLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            ExpandHwndsAsync();
        }

        private void OnConnectorUnloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            CollapseHwnds(true);
        }

        public void AddHost(IHwndHolder hwndHolder)
        {
            if (hwndHolder == null)
                throw new ArgumentNullException("hwndHolder");

            if (!m_hwndHolders.Contains(hwndHolder))
            {
                m_hwndHolders.Add(hwndHolder);
            }

            if (!m_expandingAsync)
            {
                // Making sure we dont accidently stay with a collapsed hwnd
                hwndHolder.ExpandHwnd();
            }
        }

        public void RemoveHost(IHwndHolder hwndHolder)
        {
            m_hwndHolders.Remove(hwndHolder);
        }

        public void CollapseHwnds(bool freezeBounds = false)
        {
            if (m_hwndHolders.Count == 0) return;

            Rect latestBounds = m_hwndHolders.Select(h => h.LatestHwndBounds).Aggregate(Rect.Union);
            m_comparer.RelativeTo = latestBounds.BottomRight;

            foreach (var hostHolder in m_hwndHolders.OrderBy(h => h.LatestHwndBounds.BottomRight, m_comparer))
            {
                hostHolder.CollapseHwnd(freezeBounds);
            }
        }

        public void FreezeHwndsBounds()
        {
            if (m_hwndHolders.Count == 0) return;

            foreach (var hostHolder in m_hwndHolders)
            {
                hostHolder.FreezeHwndBounds();
            }
        }

        public void ExpandHwnds()
        {
            if (m_hwndHolders.Count == 0) return;

            Rect latestBounds = m_hwndHolders.Select(h => h.LatestHwndBounds).Aggregate(Rect.Union);
            m_comparer.RelativeTo = latestBounds.TopLeft;

            foreach (var hostHolder in m_hwndHolders.OrderBy(h => h.LatestHwndBounds.TopLeft, m_comparer))
            {
                hostHolder.ExpandHwnd();
            }
        }

        public void RefreshHwndsAsync()
        {
            CollapseHwnds(true);
            ExpandHwndsAsync();
        }

        public void ExpandHwndsAsync()
        {
            if (m_expandingAsync) return;
            m_expandingAsync = true;

            DispatchUI.OnUIThreadAsync(
                () =>
                {
                    ExpandHwnds();
                    m_expandingAsync = false;
                },
                DispatcherPriority.Input
                );
        }

        public void Dispose()
        {
            m_connector.Loaded -= OnConnectorLoaded;
            m_connector.Unloaded -= OnConnectorUnloaded;
        }

        private class PointDistanceComparer : IComparer<Point>
        {
            public Point RelativeTo { get; set; }

            public int Compare(Point p1, Point p2)
            {
                double p1_distance = Math.Sqrt(Math.Pow(p1.X - RelativeTo.X, 2) + Math.Pow(p1.Y - RelativeTo.Y, 2));
                double p2_distance = Math.Sqrt(Math.Pow(p2.X - RelativeTo.X, 2) + Math.Pow(p2.Y - RelativeTo.Y, 2));

                return p1_distance.CompareTo(p2_distance);
            }
        }
    }
}
