﻿using System.Collections;
using System.Windows;

namespace HwndExtensions.Adorner;

class HwndAdornerElement : FrameworkElement
{
    private readonly HwndAdorner m_hwndAdorner;

    public HwndAdornerElement()
    {
        m_hwndAdorner = new HwndAdorner(this);

        // This helps dependency property inheritance and resource search cross the visual tree boundary
        // (between the tree containing this object and the one containing the adorner root) 
        AddLogicalChild(m_hwndAdorner.Root);
    }

    public UIElement? Adornment
    {
        get => m_hwndAdorner.Adornment;
        set => m_hwndAdorner.Adornment = value;
    }

    protected override IEnumerator LogicalChildren
    {
        get
        {
            yield return m_hwndAdorner.Root;
        }
    }
}
