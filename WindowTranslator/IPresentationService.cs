using Wpf.Ui.Controls;

namespace WindowTranslator;

partial interface IPresentationService
{
    Task<MessageBoxResult> ShowMessageAsync(MessageContext context, CancellationToken cancellationToken = default);
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
}

public record MessageContext(string Title, string Message)
{
    public string? CloseButtonText { get; init; }

    public string? PrimaryButtonText { get; init; }

    public string? SecondaryButtonText { get; init; }
}