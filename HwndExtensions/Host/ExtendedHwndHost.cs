using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace HwndExtensions.Host;

public abstract class ExtendedHwndHost : HwndHost, IHwndHolder
{
    private readonly static DependencyPropertyKey IsMouseOverHwndPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsMouseOverHwnd),
        typeof(bool),
        typeof(ExtendedHwndHost),
        new PropertyMetadata(false));

    /// <summary>Identifies the <see cref="IsMouseOverHwnd"/> dependency property.</summary>
    public readonly static DependencyProperty IsMouseOverHwndProperty = IsMouseOverHwndPropertyKey.DependencyProperty;

    public bool IsMouseOverHwnd => (bool)GetValue(IsMouseOverHwndProperty);

    static ExtendedHwndHost()
    {
        EventManager.RegisterClassHandler(typeof(ExtendedHwndHost), HwndExtensions.HwndMouseEnterEvent, new MouseEventHandler(OnHwndMouseEnterOrLeave));
        EventManager.RegisterClassHandler(typeof(ExtendedHwndHost), HwndExtensions.HwndMouseLeaveEvent, new MouseEventHandler(OnHwndMouseEnterOrLeave));
    }

    private static void OnHwndMouseEnterOrLeave(object sender, MouseEventArgs e)
    {
        var host = (ExtendedHwndHost)sender;
        host.SetValue(IsMouseOverHwndPropertyKey, e.RoutedEvent == HwndExtensions.HwndMouseEnterEvent);
    }

    /// <summary>Identifies the <see cref="ConnectsToHostManager"/> dependency property.</summary>
    public static readonly DependencyProperty ConnectsToHostManagerProperty = DependencyProperty.Register(
        nameof(ConnectsToHostManager), typeof(bool), typeof(ExtendedHwndHost), new PropertyMetadata(false, OnConnectsToHostManagerChanged));

    private static void OnConnectsToHostManagerChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs args)
    {
        var host = (ExtendedHwndHost)depObj;
        if ((bool)args.NewValue)
        {
            host.AttachConnectToHostManagerBehavior();
        }
        else
        {
            host.DettachConnectToHostManagerBehavior();
        }
    }

    public bool ConnectsToHostManager
    {
        get => (bool)GetValue(ConnectsToHostManagerProperty);
        set => SetValue(ConnectsToHostManagerProperty, value);
    }

    private void AttachConnectToHostManagerBehavior()
    {
        if (SearchForConnectBehavior() is null)
        {
            Interaction.GetBehaviors(this).Add(new ConnectToHostManagerBehavior<ExtendedHwndHost>());
        }
    }

    private void DettachConnectToHostManagerBehavior()
    {
        if (SearchForConnectBehavior() is { } connectBehavior)
        {
            Interaction.GetBehaviors(this).Remove(connectBehavior);
        }
    }

    private ConnectToHostManagerBehavior<ExtendedHwndHost>? SearchForConnectBehavior()
        => Interaction.GetBehaviors(this)
                    .OfType<ConnectToHostManagerBehavior<ExtendedHwndHost>>()
                    .FirstOrDefault();

    private Rect m_latestBounds;
    private Rect m_freezedBounds;
    private bool m_boundsAreFreezed;

    public void CollapseHwnd(bool freezeBounds = false)
    {
        if (m_boundsAreFreezed) return;

        if (freezeBounds)
        {
            FreezeHwndBounds();
        }
        else
        {
            m_boundsAreFreezed = false;
        }

        OnWindowPositionChangedOverride(new(m_latestBounds.Location, Size.Empty));
    }

    public void FreezeHwndBounds()
    {
        m_boundsAreFreezed = true;
        m_freezedBounds = m_latestBounds;
    }

    public void ExpandHwnd()
    {
        m_boundsAreFreezed = false;

        OnWindowPositionChangedOverride(m_latestBounds);
    }

    public void ExpandOnNextReposition()
    {
        m_boundsAreFreezed = false;
    }

    public Rect LatestHwndBounds => m_latestBounds;

    public Rect FreezedHwndBounds => m_freezedBounds;

    protected sealed override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        m_latestBounds = rcBoundingBox;
        if (m_boundsAreFreezed) return;

        OnWindowPositionChangedOverride(rcBoundingBox);
    }

    protected virtual void OnWindowPositionChangedOverride(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
    }
}
