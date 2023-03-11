using System;
using System.Windows.Controls;

namespace HwndExtensions.Adorner
{
    public class HwndAdornerManager : Decorator, IHwndAdornerManager, IDisposable
    {
        private readonly HwndAdornerGroup m_hwndAdornerGroup;
        private bool m_disposed;

        HwndAdornerGroup IHwndAdornerManager.AdornerGroup
        {
            get
            {
                if(m_disposed)
                    throw new ObjectDisposedException("this");
                return  m_hwndAdornerGroup;
            }
        }

        public HwndAdornerManager()
        {
            m_hwndAdornerGroup = new HwndAdornerGroup(this);
        }

        public void Dispose()
        {
            m_disposed = true;
            m_hwndAdornerGroup.Dispose();
        }
    }
}
