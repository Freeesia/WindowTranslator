using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using PropertyTools.DataAnnotations;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.AspNetCore;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules.LanguagePackInstaller;
using WindowTranslator.Modules.Startup;
using WindowTranslator.Properties;
using BrowsableAttribute = System.ComponentModel.BrowsableAttribute;
using CategoryAttribute = System.ComponentModel.CategoryAttribute;

namespace WindowTranslator.Modules.Settings;

[OpenDialog]
internal partial class SettingsViewModel : ObservableObject, IEditableObject
{
    private static readonly CompositeFormat HasUpdateText = CompositeFormat.Parse(Resources.HasUpdate);
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
    private readonly IUpdateChecker checker;

    [Browsable(false)]
    public IEnumerable<CultureInfo> Languages { get; } = [
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
    ];

    [Browsable(false)]
    public IEnumerable<ModuleItem> TranslateModules { get; }
    [Browsable(false)]
    public IEnumerable<ModuleItem> CacheModules { get; }

    [Category("SettingsViewModel|Language")]
    [ItemsSourceProperty(nameof(Languages))]
    [SelectedValuePath(nameof(CultureInfo.Name))]
    [DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    public string Source { get; set; }

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
    [FontFamilySelector]
    [FontPreview(18)]
    [SortIndex(5)]
    public string Font { get; set; }

    [property: Category("SettingsViewModel|Misc")]
    [property: Slidable(0.1, 5, 0.1, 1.0, true, 0.1)]
    [property: FormatString("F2")]
    [property: SortIndex(6)]
    [ObservableProperty]
    private double fontScale;

    [Category("SettingsViewModel|Misc")]
    public ViewMode ViewMode { get; set; }

    [Category("SettingsViewModel|Misc")]
    public OverlaySwitch OverlaySwitch { get; set; }

    [Category("SettingsViewModel|Misc")]
    public bool IsEnableCaptureOverlay { get; set; }

    [Category("TargetProcesses|")]
    public bool IsEnableAutoTarget { get; set; }

    [Category("TargetProcesses|")]
    public ObservableCollection<string> AutoTargets { get; set; } = [];

    [Comment]
    [Category("About|")]
    public string App { get; }

    [Comment]
    [Category("About|")]
    public DateTime BuildDate { get; }

    [Comment]
    [Category("About|")]
    public string DevelopedBy { get; }

    [Category("About|")]
    public Uri Link { get; }

    [Comment]
    [Category("About|")]
    public string License { get; }

    [property: Browsable(false)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateInfo))]
    private bool hasUpdate;

    [Comment]
    [PropertyTools.DataAnnotations.DisplayName("")]
    [Category("About|UpdateInfo")]
    public string UpdateInfo => this.HasUpdate ? string.Format(CultureInfo.CurrentCulture, HasUpdateText, this.checker.LatestVersion) : Resources.IsLatest;

    public IPluginParam[] Params { get; }

    public SettingsViewModel(
        [Inject] PluginProvider provider,
        [Inject] IOptionsSnapshot<CommonSettings> common,
        [Inject] IOptionsSnapshot<TargetSettings> target,
        [Inject] IEnumerable<IPluginParam> @params,
        [Inject] IServiceProvider sp,
        [Inject] IPresentationService presentationService,
        [Inject] IUpdateChecker checker)
    {
        var items = provider.GetPlugins();
        this.TranslateModules = items.Where(p => typeof(ITranslateModule).IsAssignableFrom(p.Type)).Select(Convert).ToList();
        this.CacheModules = items.Where(p => typeof(ICacheModule).IsAssignableFrom(p.Type)).Select(Convert).ToList();
        this.ViewMode = common.Value.ViewMode;
        this.AutoTargets = new(common.Value.AutoTargets);
        this.IsEnableAutoTarget = common.Value.IsEnableAutoTarget;
        this.OverlaySwitch = common.Value.OverlaySwitch;
        this.IsEnableCaptureOverlay = common.Value.IsEnableCaptureOverlay;

        var dic = target.Value.SelectedPlugins;
        this.TranslateModule = dic.TryGetValue(nameof(ITranslateModule), out var t) ? t : this.TranslateModules.OrderByDescending(i => i.IsDefault).First().Name;
        this.CacheModule = dic.TryGetValue(nameof(ICacheModule), out var c) ? c : this.CacheModules.OrderByDescending(i => i.IsDefault).First().Name;
        this.Source = target.Value.Language.Source;
        this.Target = target.Value.Language.Target;
        this.Font = target.Value.Font;
        this.FontScale = target.Value.FontScale;


        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetName();
        this.App = $"{name.Name} {name.Version}";
        this.BuildDate = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key == "BuildDateTime")?
            .Select(a => DateTime.Parse(a.Value!, CultureInfo.InvariantCulture))
            .FirstOrDefault() ?? default;
        this.DevelopedBy = "Freesia";
        this.Link = new("https://github.com/Freeesia/WindowTranslator");
        this.License = "MIT License";

        this.Params = @params.Select(p =>
        {
            var configureType = typeof(IConfigureOptions<>).MakeGenericType(p.GetType());
            var configures = (IEnumerable<object>)sp.GetService(typeof(IEnumerable<>).MakeGenericType(configureType))!;
            var configureMethod = configureType.GetMethod(nameof(IConfigureOptions<object>.Configure))!;
            foreach (var configure in configures)
            {
                configureMethod.Invoke(configure, [p]);
            }
            return p;
        }).ToArray();
        this.presentationService = presentationService;
        this.checker = checker;
        this.HasUpdate = checker.HasUpdate;
    }

    private void Checker_UpdateAvailable(object? sender, EventArgs e)
        => this.HasUpdate = this.checker.HasUpdate;

    private static ModuleItem Convert(Plugin plugin)
        => new(plugin.Type.Name, plugin.Name, plugin.Type.IsDefined(typeof(DefaultModuleAttribute)));

    [property: Category("SettingsViewModel|Misc")]
    [property: SortIndex(10)]
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

    [property: Category("SettingsViewModel|Misc")]
    [property: SortIndex(10)]
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

    [property: Category("About|")]
    [RelayCommand]
    public static void OpenThirdPartyLicenses()
    {
        var dir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "licenses");
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" \"{dir}\"") { CreateNoWindow = true });
    }

    [property: Category("About|UpdateInfo")]
    [property: VisibleBy(nameof(HasUpdate))]
    [RelayCommand]
    public void OpenChangelog()
        => this.checker.OpenChangelog();

    [property: Category("About|UpdateInfo")]
    [property: VisibleBy(nameof(HasUpdate))]
    [RelayCommand]
    public void Update()
        => this.checker.Update();

    [property: Category("About|UpdateInfo")]
    [property: VisibleBy(nameof(HasUpdate), false)]
    [RelayCommand]
    public Task CheckUpdateAsync()
        => this.checker.CheckAsync();

    public void BeginEdit()
        => this.checker.UpdateAvailable += Checker_UpdateAvailable;

    public void CancelEdit()
        => this.checker.UpdateAvailable -= Checker_UpdateAvailable;

    public void EndEdit()
    {
        this.checker.UpdateAvailable -= Checker_UpdateAvailable;
        if (!LanguagePackUtility.IsInstalledLanguage(this.Source) &&
            !LanguagePackUtility.InstallLanguageWithDialog(this.Source))
        {
            return;
        }
        var settings = new UserSettings()
        {
            Common = new()
            {
                ViewMode = this.ViewMode,
                AutoTargets = this.AutoTargets,
                IsEnableAutoTarget = this.IsEnableAutoTarget,
                OverlaySwitch = this.OverlaySwitch,
                IsEnableCaptureOverlay = this.IsEnableCaptureOverlay,
            },
            Targets =
            {
                [string.Empty] = new()
                {
                    Language = { Source = this.Source, Target = this.Target },
                    Font = this.Font,
                    FontScale = this.FontScale,
                    SelectedPlugins =
                    {
                        [nameof(ITranslateModule)] = this.TranslateModule,
                        [nameof(ICacheModule)] = this.CacheModule,
                    },
                    PluginParams = this.Params.ToDictionary(p => p.GetType().Name, p => p),
                }
            },
        };
        Directory.CreateDirectory(PathUtility.UserDir);
        using var fs = File.Open(PathUtility.UserSettings, FileMode.Create, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(fs, settings, serializerOptions);
    }
}

public record ModuleItem(string Name, string DisplayName, bool IsDefault);
