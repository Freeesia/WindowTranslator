using CommunityToolkit.Mvvm.Input;
using Cysharp.Diagnostics;
using Kamishibai;
using Microsoft.Extensions.Options;
using PropertyTools.DataAnnotations;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Weikio.PluginFramework.AspNetCore;
using BrowsableAttribute = System.ComponentModel.BrowsableAttribute;
using CategoryAttribute = System.ComponentModel.CategoryAttribute;

namespace WindowTranslator.Modules.Settings;

[OpenDialog]
internal partial class SettingsViewModel : IEditableObject
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
    };

    [Browsable(false)]
    public IReadOnlyList<CultureInfo> SupportedLanguages { get; } = new List<CultureInfo>()
    {
        CultureInfo.GetCultureInfo("ja-JP"),
        CultureInfo.GetCultureInfo("en-US"),
        CultureInfo.GetCultureInfo("zh-CN"),
    };

    [Browsable(false)]
    public IReadOnlyList<string> TranslateModules { get; }
    [Browsable(false)]
    public IReadOnlyList<string> CacheModules { get; }

    [Category("全体設定|プラグイン設定")]
    [ItemsSourceProperty(nameof(TranslateModules))]
    public string TranslateModule { get; set; }
    [Category("全体設定|プラグイン設定")]
    [ItemsSourceProperty(nameof(CacheModules))]
    public string CacheModule { get; set; }

    [Category("全体設定|言語設定")]
    [ItemsSourceProperty(nameof(SupportedLanguages))]
    [SelectedValuePath(nameof(CultureInfo.Name))]
    [DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    public string Source { get; set; }

    [Category("全体設定|言語設定")]
    [ItemsSourceProperty(nameof(SupportedLanguages))]
    [SelectedValuePath(nameof(CultureInfo.Name))]
    [DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    public string Target { get; set; }

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

    [RelayCommand]
    private async Task InstallLanguageAsync()
    {
        InstallLanguage(this.Source);
        InstallLanguage(this.Target);
    }

    private void InstallLanguage(string target)
    {
        using var ps = PowerShell.Create();
        ps.AddCommand("Set-ExecutionPolicy")
            .AddArgument("Bypass")
            .AddParameter("Scope", "Process")
            .AddParameter("Force")
            .Invoke();

        ps.AddCommand("Install-Language")
            .AddParameter("Language", target)
            .AddParameter("ExcludeFeatures")
            .Invoke();

        var results = ps.AddCommand("Get-Process").Invoke();
        Debug.WriteLine(results);
    }
    //private async Task InstallLanguageAsync(string language)
    //{
    //    var info = new ProcessStartInfo("cmd", $"/c powershell install-language {language}")
    //    {
    //        UseShellExecute = true,
    //        Verb = "RunAs",
    //    };
    //    await ProcessX.StartAsync(info).WaitAsync();
    //}
}
