using System.Globalization;
using System.Reflection;

namespace WindowTranslator;
public class AppInfo
{
    static AppInfo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetName();
        Title = $"{name.Name} {name.Version}" ?? "WindowTranslator";
        Version = name.Version ?? new Version();
        BuildDate = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key == "BuildDateTime")?
            .Select(a => DateTime.Parse(a.Value!, CultureInfo.InvariantCulture))
            .FirstOrDefault() ?? default;
    }

    public static string Title { get; }
    public static Version Version { get; }

    public static DateTime BuildDate { get; }

    public static string DevelopedBy { get; } = "Freesia";

    public static Uri Link { get; } = new("https://wt.studiofreesia.com/");

    public static string License { get; } = "MIT License";
}
