using System.Globalization;

namespace WindowTranslator.Plugin.OrcaRouterPlugin.Properties;

internal static class Resources
{
    private static readonly CustomResourceManager manager = new(typeof(Resources));

    public static string Text(string name) => manager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
