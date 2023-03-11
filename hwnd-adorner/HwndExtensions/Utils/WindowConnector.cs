using System.Windows;
using System.Windows.Interop;

namespace HwndExtensions.Utils
{
    public abstract class WindowConnector : HwndSourceConnector
    {
        protected WindowConnector(UIElement connector) : base(connector)
        {
            
        }

        protected sealed override void OnSourceConnected(HwndSource connectedSource)
        {
            var window = connectedSource.RootVisual as Window;
            if (window != null)
            {
                OnWindowConnected(window);
            }
        }

        protected sealed override void OnSourceDisconnected(HwndSource disconnectedSource)
        {
            var window = disconnectedSource.RootVisual as Window;
            if (window != null)
            {
                OnWindowDisconnected(window);
            }
        }

        protected abstract void OnWindowDisconnected(Window disconnectedWindow);
        protected abstract void OnWindowConnected(Window connectedWindow);
    }
}
