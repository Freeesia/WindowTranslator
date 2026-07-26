using System.Runtime.CompilerServices;

namespace WindowTranslator.Plugin.BergamotTranslatorPlugin;

public static partial class SystemUtility
{
    public static string ModelsPath { get; } = Path.Combine(PathUtility.SharedDir, "bergamot");

    private static void MigrateModelsIfNeeded()
    {
        var oldPath = Path.Combine(PathUtility.UserDir, "bergamot");
        if (Directory.Exists(oldPath) && !Directory.Exists(ModelsPath))
        {
            Directory.CreateDirectory(PathUtility.SharedDir);
            Directory.Move(oldPath, ModelsPath);
        }
    }

    [ModuleInitializer]
    internal static void Initialize()
    {
        MigrateModelsIfNeeded();
    }
}
