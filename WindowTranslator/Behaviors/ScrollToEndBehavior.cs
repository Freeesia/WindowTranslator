using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace WindowTranslator.Behaviors;

/// <summary>
/// ListBoxの最後の要素に常にフォーカスするBehavior
/// </summary>
public sealed class ScrollToEndBehavior : Behavior<ListBox>
{

    public bool IsAutoScrollEnabled
    {
        get => (bool)GetValue(IsAutoScrollEnabledProperty);
        set => SetValue(IsAutoScrollEnabledProperty, value);
    }

    // Using a DependencyProperty as the backing store for IsAutoScrollEnabled.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty IsAutoScrollEnabledProperty =
        DependencyProperty.Register(nameof(IsAutoScrollEnabled), typeof(bool), typeof(ScrollToEndBehavior), new PropertyMetadata(true));

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.Loaded -= OnLoaded;
        UnsubscribeFromCollectionChanged();
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SubscribeToCollectionChanged();
    }

    private void SubscribeToCollectionChanged()
    {
        if (AssociatedObject.Items is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnCollectionChanged;
        }
    }

    private void UnsubscribeFromCollectionChanged()
    {
        if (AssociatedObject.Items is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnCollectionChanged;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsAutoScrollEnabled) return;
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset && AssociatedObject.Items.Count > 0)
        {
            // UIスレッドで実行
            _ = AssociatedObject.Dispatcher.BeginInvoke(() => AssociatedObject.ScrollIntoView(AssociatedObject.Items[^1]));
        }
    }
}