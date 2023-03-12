using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowTranslator;
public class UserSettings
{
    public LanguageOptions Language { get; set; } = new();
    public Dictionary<string, string> SelectedPlugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, object> PluginParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
