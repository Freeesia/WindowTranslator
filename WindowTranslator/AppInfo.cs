using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;

namespace WindowTranslator;

public partial class AppInfo
{
    public static AppInfo Instance { get; } = new();

    [Category("Application")]
    public string Title { get; }

    [Category("Application")]
    public Version Version { get; }

    [Category("Application")]
    public DateTime BuildDate { get; }

    [Category("Application")]
    public Uri WebSite { get; } = new("https://wt.studiofreesia.com/");

    [Category("Address")]
    public string DevelopedBy { get; } = "Freesia";

    [Category("Address")]
    [Description("@WindowTrans")]
    public Uri X { get; } = new("https://x.com/WindowTrans");

    [Category("Address")]
    [Description("@WindowTrans")]
    public Uri Mond { get; } = new("https://mond.how/ja/WindowTrans");

    [Category("Address")]
    [Description("Freesia")]
    public Uri Steam { get; } = new("https://steamcommunity.com/profiles/76561198182917794/");

    [Category("Address")]
    [Description("Wishlist")]
    public Uri SteamWishlist { get; } = new("https://steamcommunity.com/profiles/76561198182917794/wishlist/");

    [Category("Develop")]
    [Description("MIT License")]
    public Uri License { get; } = new("https://github.com/Freeesia/WindowTranslator/blob/master/LICENSE");

    private AppInfo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetName();
        Title = $"{name.Name} {name.Version?.ToString(3)}" ?? "WindowTranslator";
        Version = name.Version ?? new Version();
        BuildDate = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key == "BuildDateTime")?
            .Select(a => DateTime.Parse(a.Value!, CultureInfo.InvariantCulture))
            .FirstOrDefault() ?? default;
    }

    [property: Category("Develop")]
    [RelayCommand]
    public static void OpenThirdPartyLicenses()
    {
        var dir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "licenses");
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" \"{dir}\"") { CreateNoWindow = true });
    }
}
