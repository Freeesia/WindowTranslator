using System.IO;

namespace WindowTranslator;
public static class PathUtility
{
    public static string UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wt");
    public static string UserConfig = Path.Combine(UserDir, "config.json");
}
