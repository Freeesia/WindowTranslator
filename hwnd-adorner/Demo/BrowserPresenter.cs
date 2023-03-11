using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using HwndExtensions.Host;

namespace Demo
{
    public class BrowserPresenter : HwndHostPresenter
    {
        public BrowserPresenter()
        {
            var browser = new WebBrowser();
            browser.Source = new Uri("http://blogs.microsoft.co.il/michaels/2014/11/28/wpf-hwnd-adorner/");

            HwndHost = browser;
            RegisterToAppShutdown();
        }

        private void RegisterToAppShutdown()
        {
            Application.Current.Dispatcher.ShutdownStarted += OnShutdownStarted;
        }

        private void OnShutdownStarted(object sender, EventArgs e)
        {
            Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var host = HwndHost;
                if(host != null)
                    host.Dispose();
            }
        }
    }
}
