using Kamishibai;

namespace WindowTranslator.Extensions;
public static class KamishibaiExtensions
{
    public static Task<bool> CloseAsync(this IWindow window)
    {
        var taskSource = new TaskCompletionSource<bool>();
        var count = 0;
        window.Closing += (_, e) =>
        {
            if (e.Cancel && count > 1)
            {
                taskSource.SetResult(false);
            }
            count++;
        };
        window.Closed += (_, _) =>
        {
            taskSource.SetResult(true);
        };
        window.Close();
        return taskSource.Task;
    }

    public static async ValueTask OpenErrorDialogAsync(this IPresentationService presentationService, string err, Exception ex, string target, string imagePath, object? owner = null)
    {
        if (ex is AppUserException || (ex is AggregateException { InnerExceptions: var exs } && exs.OfType<AppUserException>().Any()))
        {
            presentationService.ShowMessage(ex.Message, target, icon: MessageBoxImage.Exclamation, owner: owner);
        }
        else
        {
            await presentationService.OpenErrorReportDialogAsync(err, ex, target, imagePath, owner, new() { WindowStartupLocation = WindowStartupLocation.CenterOwner });
        }
    }

}
