using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PropertyTools.Wpf;
using WindowTranslator.ComponentModel;

namespace WindowTranslator.Modules.Settings;

internal sealed class DynamicItemsComboBox : ComboBox
{
    private readonly PropertyItem property;
    private readonly IDynamicItemsSource source;
    private CancellationTokenSource? refreshCancellation;

    public DynamicItemsComboBox(PropertyItem property, IDynamicItemsSource source)
    {
        this.property = property;
        this.source = source;
        this.DisplayMemberPath = nameof(DynamicItem.DisplayName);
        this.SelectedValuePath = nameof(DynamicItem.Value);
        this.SetBinding(SelectedValueProperty, property.CreateBinding(UpdateSourceTrigger.PropertyChanged));
        this.Loaded += Refresh;
        this.DropDownOpened += Refresh;
        this.Unloaded += (_, _) => this.refreshCancellation?.Cancel();
    }

    private async void Refresh(object? sender, EventArgs e)
    {
        this.refreshCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        this.refreshCancellation = cancellation;
        try
        {
            var items = await this.source.GetItemsAsync(this.property.Descriptor.Name, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            // 更新によって SelectedValue が一時的に null になっても、保存値には書き戻さない。
            var selected = this.property.Descriptor.GetValue(this.source);
            var candidates = items.ToList();
            if (selected is not null && !candidates.Any(i => Equals(i.Value, selected)))
            {
                candidates.Add(new(selected, selected.ToString() ?? string.Empty));
            }
            BindingOperations.ClearBinding(this, SelectedValueProperty);
            this.SetCurrentValue(ItemsSourceProperty, candidates);
            this.SetBinding(SelectedValueProperty, this.property.CreateBinding(UpdateSourceTrigger.PropertyChanged));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // 候補取得に失敗した場合は、現在の候補と選択値を維持する。
            System.Diagnostics.Trace.WriteLine("Failed to refresh dynamic items.");
        }
        finally
        {
            if (ReferenceEquals(this.refreshCancellation, cancellation))
            {
                this.refreshCancellation = null;
            }
        }
    }
}
