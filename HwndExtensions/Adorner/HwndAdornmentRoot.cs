using System.Windows;
using System.Windows.Controls;

namespace HwndExtensions.Adorner
{
    internal class HwndAdornmentRoot : ContentControl
    {
        public DependencyObject UIParentCore { get; set; }

        protected override DependencyObject GetUIParentCore()
        {
            return UIParentCore ?? base.GetUIParentCore();
        }
    }
}
