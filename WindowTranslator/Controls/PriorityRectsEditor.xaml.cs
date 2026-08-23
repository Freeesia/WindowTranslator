using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowTranslator.Controls;

/// <summary>
/// OCR対象範囲のリストを編集するコントロール
/// </summary>
public partial class PriorityRectsEditor : UserControl
{
    /// <summary>編集対象のOCR対象範囲リスト</summary>
    public IList<PriorityRect>? Rects
    {
        get => (IList<PriorityRect>?)GetValue(RectsProperty);
        set => SetValue(RectsProperty, value);
    }

    /// <summary>Identifies the <see cref="Rects"/> dependency property.</summary>
    public static readonly DependencyProperty RectsProperty =
        DependencyProperty.Register(nameof(Rects), typeof(IList<PriorityRect>), typeof(PriorityRectsEditor), new PropertyMetadata(null, OnRectsChanged));

    /// <summary>矩形選択の対象となるウィンドウのハンドル</summary>
    public nint TargetWindowHandle
    {
        get => (nint)GetValue(TargetWindowHandleProperty);
        set => SetValue(TargetWindowHandleProperty, value);
    }

    /// <summary>Identifies the <see cref="TargetWindowHandle"/> dependency property.</summary>
    public static readonly DependencyProperty TargetWindowHandleProperty =
        DependencyProperty.Register(nameof(TargetWindowHandle), typeof(nint), typeof(PriorityRectsEditor), new PropertyMetadata(IntPtr.Zero, OnTargetWindowHandleChanged));

    private readonly ObservableCollection<PriorityRectItem> items = [];
    private bool isSyncing;

    public PriorityRectsEditor()
    {
        InitializeComponent();
        this.RectList.SetCurrentValue(ItemsControl.ItemsSourceProperty, this.items);
        this.items.CollectionChanged += OnItemsChanged;
        UpdateButtonState();
    }

    private static void OnRectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PriorityRectsEditor)d).LoadRects(e.NewValue as IList<PriorityRect>);

    private static void OnTargetWindowHandleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PriorityRectsEditor)d).UpdateButtonState();

    private void LoadRects(IList<PriorityRect>? rects)
    {
        this.isSyncing = true;
        try
        {
            foreach (var item in this.items)
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
            this.items.Clear();
            foreach (var rect in rects ?? [])
            {
                var item = PriorityRectItem.From(rect);
                item.PropertyChanged += OnItemPropertyChanged;
                this.items.Add(item);
            }
        }
        finally
        {
            this.isSyncing = false;
        }
        UpdateButtonState();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.OldItems?.Cast<PriorityRectItem>() ?? [])
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        foreach (var item in e.NewItems?.Cast<PriorityRectItem>() ?? [])
        {
            item.PropertyChanged -= OnItemPropertyChanged;
            item.PropertyChanged += OnItemPropertyChanged;
        }
        SyncToSource();
        UpdateButtonState();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => SyncToSource();

    /// <summary>
    /// 編集内容を<see cref="Rects"/>へ反映する
    /// </summary>
    /// <remarks>
    /// 設定の保存はリストのインスタンスを参照するため、インスタンスを差し替えずに中身を書き換える
    /// </remarks>
    private void SyncToSource()
    {
        if (this.isSyncing || Rects is not { } rects)
        {
            return;
        }
        rects.Clear();
        foreach (var item in this.items)
        {
            rects.Add(item.ToPriorityRect());
        }
    }

    private void UpdateButtonState()
    {
        this.AddButton.SetCurrentValue(IsEnabledProperty, TargetWindowHandle != IntPtr.Zero);
        this.AddButton.SetCurrentValue(ToolTipProperty, TargetWindowHandle != IntPtr.Zero ? null : Properties.Resources.PriorityRectTargetNotFound);
    }

    private void RectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateButtonState();

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new RectangleSelectionWindow(TargetWindowHandle) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true && window.SelectedRect is { } rect)
        {
            this.items.Add(PriorityRectItem.From(rect));
            this.RectList.SetCurrentValue(Selector.SelectedIndexProperty, this.items.Count - 1);
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var item = (FrameworkElement)sender;
        var index = this.items.IndexOf((PriorityRectItem)item.DataContext);
        this.items.RemoveAt(index);
        this.RectList.SetCurrentValue(Selector.SelectedIndexProperty, Math.Min(index, this.items.Count - 1));
    }
}

/// <summary>
/// 編集中のOCR対象範囲
/// </summary>
public sealed partial class PriorityRectItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double x;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double y;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double width;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double height;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string keyword = string.Empty;

    /// <summary>リストに表示する文字列</summary>
    public string DisplayText => $"({X:P1}, {Y:P1}) {Width:P1} x {Height:P1}"
        + (string.IsNullOrWhiteSpace(Keyword) ? string.Empty : $" [{Keyword}]");

    public static PriorityRectItem From(PriorityRect rect)
        => new() { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height, Keyword = rect.Keyword };

    public PriorityRect ToPriorityRect()
        => new(X, Y, Width, Height, Keyword);
}

/// <summary>
/// 値が<see langword="null"/>でないかどうかを表す<see cref="bool"/>へ変換する
/// </summary>
[ValueConversion(typeof(object), typeof(bool))]
public sealed class NotNullToBooleanConverter : IValueConverter
{
    public static NotNullToBooleanConverter Default { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
