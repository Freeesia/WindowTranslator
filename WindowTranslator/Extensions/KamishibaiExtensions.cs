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
}
