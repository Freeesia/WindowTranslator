﻿using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PropertyTools.DataAnnotations;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Weikio.PluginFramework.AspNetCore;
using BrowsableAttribute = System.ComponentModel.BrowsableAttribute;
using CategoryAttribute = System.ComponentModel.CategoryAttribute;

namespace WindowTranslator.Modules.Settings;

[OpenDialog]
internal class SettingsViewModel : IEditableObject
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
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
    public IEnumerable<string> TranslateModules { get; }
    [Browsable(false)]
    public IEnumerable<string> CacheModules { get; }

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
    public string TranslateModule { get; set; }
    [Category("全体設定|プラグイン設定")]
    [ItemsSourceProperty(nameof(CacheModules))]
    public string CacheModule { get; set; }

    public SettingsViewModel([Inject] PluginProvider provider, [Inject] IOptionsSnapshot<LanguageOptions> langOptions)
    {
        var items = provider.GetPlugins();
        this.TranslateModules = items.Where(p => typeof(ITranslateModule).IsAssignableFrom(p.Type)).Select(p => p.Name).ToList();
        this.CacheModules = items.Where(p => typeof(ICacheModule).IsAssignableFrom(p.Type)).Select(p => p.Name).ToList();
        this.Source = langOptions.Value.Source;
        this.Target = langOptions.Value.Target;
    }

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
            Language = { Source = this.Source, Target = this.Target }
        };
        using var fs = File.Open(PathUtility.UserSettings, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(fs, settings, serializerOptions);
    }
}
