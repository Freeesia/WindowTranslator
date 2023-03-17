using Kamishibai;
using Microsoft.Extensions.Options;
using PropertyTools.DataAnnotations;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.AspNetCore;
using BrowsableAttribute = System.ComponentModel.BrowsableAttribute;
using CategoryAttribute = System.ComponentModel.CategoryAttribute;

namespace WindowTranslator.Modules.Settings;

[OpenDialog]
internal class SettingsViewModel : IEditableObject
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Converters =
        {
            new PluginParamConverter(),
        },
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
    };

    [Browsable(false)]
    public IEnumerable<CultureInfo> Languages { get; } = new[]
    {
        CultureInfo.GetCultureInfo("ja-JP"),
        CultureInfo.GetCultureInfo("en-US"),
        CultureInfo.GetCultureInfo("pt-BR"),
        CultureInfo.GetCultureInfo("fr-CA"),
        CultureInfo.GetCultureInfo("fr-FR"),
        CultureInfo.GetCultureInfo("it-IT"),
        CultureInfo.GetCultureInfo("de-DE"),
        CultureInfo.GetCultureInfo("es-ES"),
        CultureInfo.GetCultureInfo("pt-PT"),
        CultureInfo.GetCultureInfo("nl-NL"),
        CultureInfo.GetCultureInfo("ru-RU"),
        CultureInfo.GetCultureInfo("ko-KR"),
        CultureInfo.GetCultureInfo("zh-CN"),
        CultureInfo.GetCultureInfo("zh-TW"),
    };

    [Browsable(false)]
    public IEnumerable<ModuleItem> TranslateModules { get; }
    [Browsable(false)]
    public IEnumerable<ModuleItem> CacheModules { get; }

    [Category("全体設定|言語設定")]
    [ItemsSourceProperty(nameof(Languages))]
    [SelectedValuePath(nameof(CultureInfo.Name))]
    [DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    public string Source { get; set; }

    [Category("全体設定|言語設定")]
    [ItemsSourceProperty(nameof(Languages))]
    [SelectedValuePath(nameof(CultureInfo.Name))]
    [DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    public string Target { get; set; }

    [Category("全体設定|プラグイン設定")]
    [ItemsSourceProperty(nameof(TranslateModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string TranslateModule { get; set; }

    [Category("全体設定|プラグイン設定")]
    [ItemsSourceProperty(nameof(CacheModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string CacheModule { get; set; }

    [Category("プラグイン設定|")]
    public IPluginParam[] Params { get; }

    public SettingsViewModel([Inject] PluginProvider provider, [Inject] IOptionsSnapshot<UserSettings> userSettings, [Inject] IEnumerable<IPluginParam> @params, [Inject] IServiceProvider sp)
    {
        var items = provider.GetPlugins();
        this.TranslateModules = items.Where(p => typeof(ITranslateModule).IsAssignableFrom(p.Type)).Select(Convert).ToList();
        this.CacheModules = items.Where(p => typeof(ICacheModule).IsAssignableFrom(p.Type)).Select(Convert).ToList();
        var dic = userSettings.Value.SelectedPlugins;
        this.TranslateModule = dic.TryGetValue(nameof(ITranslateModule), out var t) ? t : this.TranslateModules.OrderByDescending(i => i.IsDefault).First().Name;
        this.CacheModule = dic.TryGetValue(nameof(ICacheModule), out var c) ? c : this.CacheModules.OrderByDescending(i => i.IsDefault).First().Name;
        this.Source = userSettings.Value.Language.Source;
        this.Target = userSettings.Value.Language.Target;
        this.Params = @params.Select(p =>
        {
            var configureType = typeof(IConfigureOptions<>).MakeGenericType(p.GetType());
            var configures = (IEnumerable<object>)sp.GetService(typeof(IEnumerable<>).MakeGenericType(configureType))!;
            var configureMethod = configureType.GetMethod(nameof(IConfigureOptions<object>.Configure))!;
            foreach (var configure in configures)
            {
                configureMethod.Invoke(configure, new[] { p });
            }
            return p;
        }).ToArray();
    }

    private static ModuleItem Convert(Plugin plugin)
        => new(plugin.Type.Name, plugin.Name, plugin.Type.IsDefined(typeof(DefaultModuleAttribute)));

    public void BeginEdit()
    {
    }

    public void CancelEdit()
    {
    }

    public void EndEdit()
    {
        var settings = new UserSettings()
        {
            Language = { Source = this.Source, Target = this.Target },
            SelectedPlugins =
            {
                [nameof(ITranslateModule)] = this.TranslateModule,
                [nameof(ICacheModule)] = this.CacheModule,
            },
            PluginParams = this.Params.ToDictionary(p => p.GetType().Name, p => p),
        };
        Directory.CreateDirectory(PathUtility.UserDir);
        using var fs = File.Open(PathUtility.UserSettings, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(fs, settings, serializerOptions);
    }
}

public record ModuleItem(string Name, string DisplayName, bool IsDefault);
