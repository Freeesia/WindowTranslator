using System;
using System.Windows;
using System.Windows.Interop;

namespace HwndExtensions.Utils
{
    /// <summary>
    /// A class for managing the connection of an UIElement to its HwndSouce container.
    /// Notifying on any HwndSouce change.
    /// </summary>
    public abstract class HwndSourceConnector : IDisposable
    {
        private readonly UIElement m_connector;
        private bool m_activated;

        protected HwndSourceConnector(UIElement connector)
        {
            m_connector = connector;
        }

        protected void Activate()
        {
            if (m_activated) return;

            var hwndSource = PresentationSource.FromVisual(m_connector) as HwndSource;
            if (hwndSource != null)
            {
                OnSourceConnected(hwndSource);
            }

            PresentationSource.AddSourceChangedHandler(m_connector, OnSourceChanged);
            m_activated = true;
        }

        protected void Deactivate()
        {
            if (!m_activated) return;

            var hwndSource = PresentationSource.FromVisual(m_connector) as HwndSource;
            if (hwndSource != null)
            {
                OnSourceDisconnected(hwndSource);
            }

            PresentationSource.RemoveSourceChangedHandler(m_connector, OnSourceChanged);
            m_activated = false;
        }

        protected bool Activated
        {
            get { return m_activated; }
        }

        private void OnSourceChanged(object sender, SourceChangedEventArgs args)
        {
            var hwndSource = args.OldSource as HwndSource;
            if (hwndSource != null)
            {
                OnSourceDisconnected(hwndSource);
            }

            hwndSource = args.NewSource as HwndSource;
            if (hwndSource != null)
            {
                OnSourceConnected(hwndSource);
            }
        }

        protected abstract void OnSourceDisconnected(HwndSource disconnectedSource);
        protected abstract void OnSourceConnected(HwndSource connectedSource);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Deactivate();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
