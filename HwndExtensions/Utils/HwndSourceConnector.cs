using System.Windows;
using System.Windows.Interop;

namespace HwndExtensions.Utils;

/// <summary>
/// A class for managing the connection of an UIElement to its HwndSouce container.
/// Notifying on any HwndSouce change.
/// </summary>
public abstract class HwndSourceConnector : IDisposable
{
    private readonly UIElement m_connector;

    protected bool Activated { get; private set; }

    protected HwndSourceConnector(UIElement connector)
    {
        m_connector = connector;
    }

    protected void Activate()
    {
        if (Activated) return;

        if (PresentationSource.FromVisual(m_connector) is HwndSource hwndSource)
        {
            OnSourceConnected(hwndSource);
        }

        PresentationSource.AddSourceChangedHandler(m_connector, OnSourceChanged);
        Activated = true;
    }

    protected void Deactivate()
    {
        if (!Activated) return;

        if (PresentationSource.FromVisual(m_connector) is HwndSource hwndSource)
        {
            OnSourceDisconnected(hwndSource);
        }

        PresentationSource.RemoveSourceChangedHandler(m_connector, OnSourceChanged);
        Activated = false;
    }


    private void OnSourceChanged(object sender, SourceChangedEventArgs args)
    {
        if (args.OldSource is HwndSource oldSource)
        {
            OnSourceDisconnected(oldSource);
        }

        if (args.NewSource is HwndSource newSource)
        {
            OnSourceConnected(newSource);
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
