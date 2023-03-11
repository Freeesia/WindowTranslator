﻿using System.Windows;
using System.Windows.Input;
using HwndExtensions.Adorner;

namespace HwndExtensions;

public static class HwndExtensions
{
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


    // Manages an adornment over any FrameworkElement through a private attached property
    // containning the HwndAdorner instance which will present the adornment over hwnd's
    private static readonly DependencyProperty HwndAdornerProperty = DependencyProperty.RegisterAttached(
        "HwndAdorner", typeof(HwndAdorner), typeof(HwndExtensions), new PropertyMetadata(null));

    private static void SetHwndAdorner(DependencyObject element, HwndAdorner? value)
        => element.SetValue(HwndAdornerProperty, value);

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    private static HwndAdorner? GetHwndAdorner(DependencyObject element)
        => (HwndAdorner?)element.GetValue(HwndAdornerProperty);

    public static readonly DependencyProperty HwndAdornmentProperty = DependencyProperty.RegisterAttached(
        "HwndAdornment", typeof(UIElement), typeof(HwndExtensions), new UIPropertyMetadata(null, OnHwndAdornmentChanged));

    /// <summary>Helper for setting <see cref="HwndAdornmentProperty"/> on <paramref name="element"/>.</summary>
    /// <param name="element"><see cref="DependencyObject"/> to set <see cref="HwndAdornmentProperty"/> on.</param>
    /// <param name="value">HwndAdornment property value.</param>
    public static void SetHwndAdornment(DependencyObject element, UIElement? value)
        => element.SetValue(HwndAdornmentProperty, value);

    /// <summary>Helper for getting <see cref="HwndAdornmentProperty"/> from <paramref name="element"/>.</summary>
    /// <param name="element"><see cref="DependencyObject"/> to read <see cref="HwndAdornmentProperty"/> from.</param>
    /// <returns>HwndAdornment property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static UIElement? GetHwndAdornment(DependencyObject element)
        => (UIElement?)element.GetValue(HwndAdornmentProperty);

    private static void OnHwndAdornmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        if (d is not FrameworkElement element) return;

        var adorner = GetHwndAdorner(element);

        if (args.NewValue is UIElement adornment)
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
}
