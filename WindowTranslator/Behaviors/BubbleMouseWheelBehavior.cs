using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace WindowTranslator.Behaviors;


public sealed class BubbleMouseWheelBehavior
    : Behavior<FlowDocumentScrollViewer>
{
    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject.AddHandler(
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel),
            handledEventsToo: true);
    }

    protected override void OnDetaching()
    {
        AssociatedObject.RemoveHandler(
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel));

        base.OnDetaching();
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var viewer = AssociatedObject;

        // この時点ではテンプレート適用済み
        if (viewer.Template?.FindName("PART_ContentHost", viewer) is not ScrollViewer contentHost)
            return;

        var canScroll = e.Delta switch
        {
            > 0 => contentHost.VerticalOffset > 0,
            < 0 => contentHost.VerticalOffset < contentHost.ScrollableHeight,
            _ => false,
        };

        if (canScroll)
            return;

        var parent = FindParentScrollViewer(viewer);

        if (parent is null)
            return;

        e.Handled = true;

        parent.RaiseEvent(new MouseWheelEventArgs(
            e.MouseDevice,
            e.Timestamp,
            e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = parent,
        });
    }

    private static ScrollViewer? FindParentScrollViewer(
        DependencyObject element)
    {
        for (var parent = VisualTreeHelper.GetParent(element);
             parent is not null;
             parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer scrollViewer)
                return scrollViewer;
        }

        return null;
    }
}