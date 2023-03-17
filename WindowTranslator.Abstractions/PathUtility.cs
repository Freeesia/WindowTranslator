using System.IO;

namespace WindowTranslator;
public static class PathUtility
{
    public static string UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wt");
    public static string UserSettings = Path.Combine(UserDir, "settings.json");
}
