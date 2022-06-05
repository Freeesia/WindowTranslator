using System.Windows;
using System.Windows.Interop;

namespace HwndExtensions.Utils;

public abstract class WindowConnector : HwndSourceConnector
{
    protected WindowConnector(UIElement connector) : base(connector)
    {
    }

    protected sealed override void OnSourceConnected(HwndSource connectedSource)
    {
        if (connectedSource.RootVisual is Window window)
        {
            OnWindowConnected(window);
        }
    }

    protected sealed override void OnSourceDisconnected(HwndSource disconnectedSource)
    {
        if (disconnectedSource.RootVisual is Window window)
        {
            OnWindowDisconnected(window);
        }
    }

    protected abstract void OnWindowDisconnected(Window disconnectedWindow);
    protected abstract void OnWindowConnected(Window connectedWindow);
}
