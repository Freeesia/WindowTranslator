using Microsoft.Extensions.DependencyInjection;
using WindowTranslator.Modules.Validate;
using Wpf.Ui.Controls;

namespace WindowTranslator;

partial interface IPresentationService
{
    Task<MessageBoxResult> ShowMessageAsync(MessageContext context, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ValidateResult>> OpenValidateAsync(TargetSettings settings);
}

partial class PresentationService
{
    public Task<MessageBoxResult> ShowMessageAsync(MessageContext context, CancellationToken cancellationToken = default)
    {
        var box = new MessageBox()
        {
            Title = context.Title,
            Content = context.Message,
        };
        if (!string.IsNullOrEmpty(context.CloseButtonText))
        {
            box.CloseButtonText = context.CloseButtonText;
        }
        if (!string.IsNullOrEmpty(context.PrimaryButtonText))
        {
            box.PrimaryButtonText = context.PrimaryButtonText;
        }
        if (!string.IsNullOrEmpty(context.SecondaryButtonText))
        {
            box.SecondaryButtonText = context.SecondaryButtonText;
        }
        return box.ShowDialogAsync(true, cancellationToken);
    }

    public async Task<IReadOnlyList<ValidateResult>> OpenValidateAsync(TargetSettings settings)
    {
        var viewModel = new ValidateViewModel(this, this._serviceProvider.GetServices<ITargetSettingsValidator>(), settings);
        await OpenDialogAsync(viewModel, System.Windows.Application.Current.MainWindow, new() { WindowStartupLocation = Kamishibai.WindowStartupLocation.CenterOwner });
        return viewModel.Results;
    }
}

public record MessageContext(string Title, string Message)
{
    public string? CloseButtonText { get; init; }

    public string? PrimaryButtonText { get; init; }

    public string? SecondaryButtonText { get; init; }
}