using Wpf.Ui.Controls;

namespace WindowTranslator.Modules.PluginStore;

public partial class PluginSetupDialog : ContentDialog
{
    internal PluginSetupDialog(PluginSetupViewModel viewModel)
    {
        InitializeComponent();
        this.DataContext = viewModel;
    }

    protected override async void OnButtonClick(ContentDialogButton button)
    {
        var viewModel = (PluginSetupViewModel)this.DataContext;
        if (button == ContentDialogButton.Primary)
        {
            await viewModel.InstallAsync();
        }
        else if (button == ContentDialogButton.Secondary)
        {
            await viewModel.FinishAsync();
        }
        if (viewModel.IsCompleted)
        {
            Hide(ContentDialogResult.Primary);
        }
    }

    public override void Hide(ContentDialogResult result = ContentDialogResult.None)
    {
        if (((PluginSetupViewModel)this.DataContext).IsCompleted)
        {
            base.Hide(result);
        }
    }
}
