using System.Windows;
using System.Windows.Interactivity;
using HwndExtensions.Utils;

namespace HwndExtensions.Host
{
    public class ConnectToHostManagerBehavior<T> : Behavior<T> where T : FrameworkElement, IHwndHolder
    {
        private IHwndHostManager m_hostManager;

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Unloaded += OnUnloaded;
        }

        private void ConnectToManager(IHwndHostManager manager)
        {
            if (manager != null)
            {
                m_hostManager = manager;
                m_hostManager.HwndHostGroup.AddHost(AssociatedObject);
            }

            else
            {
                m_hostManager = null;
            }
        }

        private void DisconnectFromManager()
        {
            if (m_hostManager != null)
            {
                m_hostManager.HwndHostGroup.RemoveHost(AssociatedObject);
                m_hostManager = null;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            var manager = WPFTreeExtensions.TryFindVisualAncestor<IHwndHostManager>(AssociatedObject);
            if (m_hostManager != manager)
            {
                DisconnectFromManager();
                ConnectToManager(manager);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            DisconnectFromManager();
        }

        protected override void OnDetaching()
        {
            DisconnectFromManager();

            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Unloaded -= OnUnloaded;

            base.OnDetaching();
        }
    }
}
