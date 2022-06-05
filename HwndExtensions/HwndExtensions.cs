using System.Windows;
using System.Windows.Input;
using HwndExtensions.Adorner;

namespace HwndExtensions
{
    public static class HwndExtensions
    {
        #region Mouse Enter / Leave

        public static readonly RoutedEvent HwndMouseEnterEvent = EventManager.RegisterRoutedEvent(
            "HwndMouseEnter", 
            RoutingStrategy.Bubble, 
            typeof(MouseEventHandler),
            typeof(HwndExtensions));

        public static readonly RoutedEvent HwndMouseLeaveEvent = EventManager.RegisterRoutedEvent(
            "HwndMouseLeave", 
            RoutingStrategy.Bubble, 
            typeof(MouseEventHandler),
            typeof(HwndExtensions));

        #endregion

        #region Attached HwndAdornment

        // Manages an adornment over any FrameworkElement through a private attached property
        // containning the HwndAdorner instance which will present the adornment over hwnd's

        private static readonly DependencyProperty HwndAdornerProperty = DependencyProperty.RegisterAttached(
            "HwndAdorner", typeof (HwndAdorner), typeof (HwndExtensions), new PropertyMetadata(null));

        private static void SetHwndAdorner(DependencyObject element, HwndAdorner value)
        {
            element.SetValue(HwndAdornerProperty, value);
        }

        private static HwndAdorner GetHwndAdorner(DependencyObject element)
        {
            return (HwndAdorner)element.GetValue(HwndAdornerProperty);
        }

        public static readonly DependencyProperty HwndAdornmentProperty = DependencyProperty.RegisterAttached(
            "HwndAdornment", typeof (UIElement), typeof (HwndExtensions), new UIPropertyMetadata(null, OnAdornmentAttached));

        public static void SetHwndAdornment(DependencyObject element, UIElement value)
        {
            element.SetValue(HwndAdornmentProperty, value);
        }

        public static UIElement GetHwndAdornment(DependencyObject element)
        {
            return (UIElement) element.GetValue(HwndAdornmentProperty);
        }

        private static void OnAdornmentAttached(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            var element = d as FrameworkElement;
            if(element == null) return;

            UIElement adornment = (UIElement) args.NewValue;

            var adorner = GetHwndAdorner(element);

            if (adornment != null)
            {
                if (adorner == null)
                {
                    SetHwndAdorner(element, adorner = new HwndAdorner(element));
                }

                adorner.Adornment = adornment;
            }

            else
            {
                if (adorner != null)
                {
                    adorner.Dispose();
                    SetHwndAdorner(element, null);
                }
            }
        }

        #endregion
    }
}
