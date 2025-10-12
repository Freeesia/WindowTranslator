using LLama.Abstractions;
using LLama.Native;

namespace WindowTranslator.Plugin.PLaMoPlugin;
public class LLamaSharpNativeLibrarySelectingPolicy : INativeLibrarySelectingPolicy
{
    public static LLamaSharpNativeLibrarySelectingPolicy Instance { get; } = new();

    public IEnumerable<INativeLibrary> Apply(NativeLibraryConfig.Description description, SystemInfo systemInfo, NativeLogConfig.LLamaLogCallback? logCallback = null)
    {
        Log(description.ToString(), LLamaLogLevel.Info, logCallback);
        yield return new NativeLibraryWithCuda(12, description.Library, description.AvxLevel, description.SkipCheck);
        yield return new NativeLibraryWithAvx(description.Library, description.AvxLevel, description.SkipCheck);
    }

    private static void Log(string message, LLamaLogLevel level, NativeLogConfig.LLamaLogCallback? logCallback)
    {
        if (!message.EndsWith('\n'))
            message += "\n";

        logCallback?.Invoke(level, message);
    }
}
