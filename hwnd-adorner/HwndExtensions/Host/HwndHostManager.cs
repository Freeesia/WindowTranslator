using System;
using System.Windows.Controls;

namespace HwndExtensions.Host
{
    public class HwndHostManager : Decorator, IHwndHostManager
    {
        private readonly HwndHostGroup m_hostGroup;
        private bool m_disposed;

        public HwndHostGroup HwndHostGroup
        {
            get
            {
                if(m_disposed)
                    throw new ObjectDisposedException("this");
                return  m_hostGroup;
            }
        }

        public HwndHostManager()
        {
            m_hostGroup = new HwndHostGroup(this);
        }

        public void Dispose()
        {
            m_disposed = true;
            m_hostGroup.Dispose();
        }
    }
}
