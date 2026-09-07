using System.Diagnostics;
using System.Globalization;
using System.Windows;

namespace WindowTranslator;

internal static class ApplicationRestart
{
    internal const string RestartProcessIdArgument = "--windowtranslator-restart-pid";

    public static void Restart()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("実行ファイルのパスを取得できませんでした。");
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(RestartProcessIdArgument);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        foreach (var argument in RemoveRestartArguments(Environment.GetCommandLineArgs().Skip(1)))
        {
            startInfo.ArgumentList.Add(argument);
        }

        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("WindowTranslatorを再起動できませんでした。");
        Application.Current.Shutdown();
    }

    public static string[] WaitForPreviousProcess(IEnumerable<string> arguments)
    {
        var (remainingArguments, processId) = ParseRestartArguments(arguments);

        if (processId is not null && processId != Environment.ProcessId)
        {
            try
            {
                using var process = Process.GetProcessById(processId.Value);
                process.WaitForExit();
            }
            catch (ArgumentException)
            {
                // 再起動元のプロセスはすでに終了している
            }
        }

        return remainingArguments;
    }

    internal static string[] RemoveRestartArguments(IEnumerable<string> arguments)
        => ParseRestartArguments(arguments).Arguments;

    private static (string[] Arguments, int? ProcessId) ParseRestartArguments(
        IEnumerable<string> arguments)
    {
        var argumentArray = arguments.ToArray();
        var remainingArguments = new List<string>();
        int? processId = null;
        for (var index = 0; index < argumentArray.Length; index++)
        {
            var argument = argumentArray[index];
            if (argument.Equals(RestartProcessIdArgument, StringComparison.OrdinalIgnoreCase)
                && index + 1 < argumentArray.Length
                && int.TryParse(
                    argumentArray[index + 1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsedProcessId))
            {
                processId = parsedProcessId;
                index++;
                continue;
            }

            remainingArguments.Add(argument);
        }

        return ([.. remainingArguments], processId);
    }
}
