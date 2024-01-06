using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using Microsoft.Extensions.Options;
using Microsoft.PowerShell;
using Microsoft.Win32;
using PropertyTools.DataAnnotations;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.AspNetCore;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules.Startup;
using BrowsableAttribute = System.ComponentModel.BrowsableAttribute;
using CategoryAttribute = System.ComponentModel.CategoryAttribute;

namespace WindowTranslator.Modules.Settings;

[OpenDialog]
internal partial class SettingsViewModel : ObservableObject, IEditableObject
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
    private readonly IPresentationService presentationService;

    private Task<bool> checkTask;

    [Browsable(false)]
    public IEnumerable<CultureInfo> Languages { get; } = new[]
    {
        CultureInfo.GetCultureInfo("ja-JP"),
        CultureInfo.GetCultureInfo("en-US"),
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

    [property: Category("SettingsViewModel|Language")]
    [property: ItemsSourceProperty(nameof(Languages))]
    [property: SelectedValuePath(nameof(CultureInfo.Name))]
    [property: DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    [ObservableProperty]
    private string source;

    [Category("SettingsViewModel|Language")]
    [ItemsSourceProperty(nameof(Languages))]
    [SelectedValuePath(nameof(CultureInfo.Name))]
    [DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    public string Target { get; set; }

    [Category("SettingsViewModel|Plugin")]
    [ItemsSourceProperty(nameof(TranslateModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string TranslateModule { get; set; }

    [Category("SettingsViewModel|Plugin")]
    [ItemsSourceProperty(nameof(CacheModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string CacheModule { get; set; }

    [Category("SettingsViewModel|Misc")]
    public ViewMode ViewMode { get; set; }

    [Category("SettingsViewModel|Misc")]
    public IList<ProcessName> AutoTargets { get; set; } = new List<ProcessName>();

    [Category("SettingsViewModel|Misc")]
    public bool IsEnableAutoTarget { get; set; }

    public IPluginParam[] Params { get; }

    public SettingsViewModel([Inject] PluginProvider provider, [Inject] IOptionsSnapshot<UserSettings> userSettings, [Inject] IEnumerable<IPluginParam> @params, [Inject] IServiceProvider sp, [Inject] IPresentationService presentationService)
    {
        var items = provider.GetPlugins();
        this.TranslateModules = items.Where(p => typeof(ITranslateModule).IsAssignableFrom(p.Type)).Select(Convert).ToList();
        this.CacheModules = items.Where(p => typeof(ICacheModule).IsAssignableFrom(p.Type)).Select(Convert).ToList();
        var dic = userSettings.Value.SelectedPlugins;
        this.TranslateModule = dic.TryGetValue(nameof(ITranslateModule), out var t) ? t : this.TranslateModules.OrderByDescending(i => i.IsDefault).First().Name;
        this.CacheModule = dic.TryGetValue(nameof(ICacheModule), out var c) ? c : this.CacheModules.OrderByDescending(i => i.IsDefault).First().Name;
        this.Source = userSettings.Value.Language.Source;
        this.Target = userSettings.Value.Language.Target;
        this.ViewMode = userSettings.Value.ViewMode;
        this.AutoTargets = userSettings.Value.AutoTargets.Select(t => new ProcessName() { Name = t }).ToList();
        this.IsEnableAutoTarget = userSettings.Value.IsEnableAutoTarget;
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
        this.presentationService = presentationService;
    }

    private static ModuleItem Convert(Plugin plugin)
        => new(plugin.Type.Name, plugin.Name, plugin.Type.IsDefined(typeof(DefaultModuleAttribute)));

    [RelayCommand]
    public void RegisterToStartup()
    {
        var exe = Assembly.GetExecutingAssembly();
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        string? name = exe.GetName().Name;
        if (key != null && Environment.ProcessPath is { } path)
        {
            key.SetValue(name, $"\"{path}\" --{nameof(LaunchMode)} {LaunchMode.Startup}");
            this.presentationService.ShowMessage($"{name}を自動起動に登録しました。", icon: MessageBoxImage.Information);
        }
        else
        {
            this.presentationService.ShowMessage($"{name}を自動起動に登録できませんでした。", icon: MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void UnregisterFromStartup()
    {
        var exe = Assembly.GetExecutingAssembly();
        var name = exe.GetName().Name;
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        if (name is not null && key?.GetValue(name) is not null)
        {
            key.DeleteValue(name);
            this.presentationService.ShowMessage($"{name}を自動起動を解除しました。", icon: MessageBoxImage.Information);
        }
    }

    partial void OnSourceChanged(string value)
    {
        this.checkTask = IsInstalledLanguageAsync(value);
    }

    private static async Task<bool> IsInstalledLanguageAsync(string lang)
    {
        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        ps.AddScript($"Get-InstalledLanguage -language {lang}");
        var res = await ps.InvokeAsync().ConfigureAwait(false);
        var output = (IList)res.Single().BaseObject;
        if (output.Count == 0)
        {
            return false;
        }
        var langInfo = output[0];
        var features = langInfo!.GetType().GetField("LanguageFeatures")!.GetValue(langInfo);
        return (((int)features!) & 0x20) == 0x20;
    }

    private static async Task InstallLanguage(string lang)
    {
        var defaultSessionState = InitialSessionState.CreateDefault();
        defaultSessionState.ExecutionPolicy = ExecutionPolicy.RemoteSigned;
        using var runspace = RunspaceFactory.CreateRunspace(defaultSessionState);
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        ps.AddScript($"Get-WindowsCapability -Online | Where-Object {{ $_.Name -Like 'Language.OCR*{lang}*' }} | Add-WindowsCapability -Online");
        await ps.InvokeAsync().ConfigureAwait(false);
        if (ps.HadErrors)
        {
            throw new InvalidOperationException("OCR言語パックインストール失敗", ps.Streams.Error.First().Exception);
        }
    }

    public void BeginEdit()
    {
    }

    public void CancelEdit()
    {
    }

    public void EndEdit()
    {
        if (!this.checkTask.Result)
        {
            this.presentationService.ShowMessage($"""
                翻訳元言語の{this.Source}は文字認識のために必要なOCR機能がインストールされていません。
                翻訳開始前に言語機能をインストールしてください。
                """,
                icon: MessageBoxImage.Warning);
            InstallLanguage(this.Source).Wait();
            //Process.Start(new ProcessStartInfo("cmd.exe", "/c start \"\" ms-settings:regionlanguage-adddisplaylanguage") { CreateNoWindow = true });
        }
        var settings = new UserSettings()
        {
            Language = { Source = this.Source, Target = this.Target },
            ViewMode = this.ViewMode,
            AutoTargets = this.AutoTargets.Select(t => t.Name).OfType<string>().ToList(),
            IsEnableAutoTarget = this.IsEnableAutoTarget,
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
public record ProcessName
{
    public string? Name { get; set; }
}
