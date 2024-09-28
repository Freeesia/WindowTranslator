using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using PropertyTools.DataAnnotations;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.AspNetCore;
using WindowTranslator.ComponentModel;
using WindowTranslator.Stores;
using Wpf.Ui;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Modules.Settings;

[OpenDialog]
sealed partial class AllSettingsViewModel : ObservableObject, IDisposable
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
    private readonly IUpdateChecker updateChecker;
    private readonly IContentDialogService dialogService;
    private readonly IPresentationService presentationService;
    private readonly IAutoTargetStore autoTargetStore;
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasUpdate;

    [ObservableProperty]
    private string? newVersion;

    [ObservableProperty]
    private bool isStartup;

    [ObservableProperty]
    private int selectedTab;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCheckableCapture))]
    private ViewMode viewMode;

    [ObservableProperty]
    private bool isEnableCaptureOverlay;

    [ObservableProperty]
    private OverlaySwitch overlaySwitch;

    [ObservableProperty]
    private bool isEnableAutoTarget;

    [ObservableProperty]
    private TargetSettingsViewModel selectedTarget;

    public string Title { get; } = $"WindowTranslator {Assembly.GetExecutingAssembly().GetName().Version}";

    public IReadOnlyList<EnumItem<ViewMode>> ViewModes { get; } = Enum.GetValues<ViewMode>().Select(v => new EnumItem<ViewMode>(v)).ToArray();

    public IReadOnlyList<EnumItem<OverlaySwitch>> OverlaySwitches { get; } = Enum.GetValues<OverlaySwitch>().Select(v => new EnumItem<OverlaySwitch>(v)).ToArray();

    public bool IsCheckableCapture => this.ViewMode == ViewMode.Overlay;

    public ObservableCollection<string> AutoTargets { get; }

    public ObservableCollection<TargetSettingsViewModel> Targets { get; }

    public Version Version { get; }

    public DateTime BuildDate { get; }

    public string DevelopedBy { get; }

    public Uri Link { get; }

    public string License { get; }

    public AllSettingsViewModel(
        [Inject] PluginProvider provider,
        [Inject] IOptionsSnapshot<UserSettings> options,
        [Inject] IServiceProvider sp,
        [Inject] IUpdateChecker updateChecker,
        [Inject] IContentDialogService dialogService,
        [Inject] IPresentationService presentationService,
        [Inject] IAutoTargetStore autoTargetStore,
        string target)
    {
        var items = provider.GetPlugins();
        var translateModules = items.Where(p => typeof(ITranslateModule).IsAssignableFrom(p.Type)).Select(Convert).ToArray();
        var cacheModules = items.Where(p => typeof(ICacheModule).IsAssignableFrom(p.Type)).Select(Convert).ToArray();

        var common = options.Value.Common;
        this.ViewMode = common.ViewMode;
        this.IsEnableCaptureOverlay = common.IsEnableCaptureOverlay;
        this.OverlaySwitch = common.OverlaySwitch;
        this.IsEnableAutoTarget = common.IsEnableAutoTarget;
        this.AutoTargets = new(autoTargetStore.AutoTargets);

        this.Targets = new(options.Value.Targets
            .DefaultIfEmpty(new KeyValuePair<string, TargetSettings>(string.Empty, new()))
            .Select(t => new TargetSettingsViewModel(t.Key, sp, t.Value, translateModules, cacheModules)));

        if (this.Targets.FirstOrDefault(t => t.Name == target) is not { } selected)
        {
            selected = new TargetSettingsViewModel(target, sp, new(), translateModules, cacheModules);
            this.Targets.Add(selected);
        }
        if (!string.IsNullOrEmpty(target))
        {
            this.SelectedTab = 1;
        }
        this.SelectedTarget = selected;

        this.updateChecker = updateChecker;
        this.dialogService = dialogService;
        this.presentationService = presentationService;
        this.autoTargetStore = autoTargetStore;
        this.updateChecker.UpdateAvailable += UpdateChecker_UpdateAvailable;
        SetUpUpdateInfo();
        this.isStartup = GetIsStartup();

        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetName();
        this.Version = name.Version ?? new Version();
        this.BuildDate = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key == "BuildDateTime")?
            .Select(a => DateTime.Parse(a.Value!, CultureInfo.InvariantCulture))
            .FirstOrDefault() ?? default;
        this.DevelopedBy = "Freesia";
        this.Link = new("https://github.com/Freeesia/WindowTranslator");
        this.License = "MIT License";
    }

    private void UpdateChecker_UpdateAvailable(object? sender, EventArgs e)
        => SetUpUpdateInfo();

    private void SetUpUpdateInfo()
    {
        if (this.updateChecker.HasUpdate)
        {
            this.NewVersion = this.updateChecker.LatestVersion;
            this.HasUpdate = true;
        }
    }

    [RelayCommand]
    public static void OpenReleaseNotes()
        => Process.Start(new ProcessStartInfo("https://github.com/Freeesia/WindowTranslator/releases/latest") { UseShellExecute = true });

    [RelayCommand]
    public Task CheckUpdateAsync(CancellationToken token)
        => this.updateChecker.CheckAsync(token);

    async partial void OnIsStartupChanged(bool value)
    {
        var exe = Assembly.GetExecutingAssembly();
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new InvalidOperationException();
        var name = exe.GetName().Name ?? throw new InvalidOperationException();
        var path = Environment.ProcessPath ?? throw new InvalidOperationException();
        if (value)
        {
            key.SetValue(name, path);
            await this.dialogService.ShowAlertAsync("自動起動", $"{name}を自動起動に登録しました。", "OK");
        }
        else
        {
            key.DeleteValue(name, false);
            await this.dialogService.ShowAlertAsync("自動起動", $"{name}の自動起動を解除しました。", "OK");
        }
    }

    private static bool GetIsStartup()
    {
        var exe = Assembly.GetExecutingAssembly();
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
        string? name = exe.GetName().Name;
        return key?.GetValue(name) is { };
    }
    private static ModuleItem Convert(Plugin plugin)
    => new(plugin.Type.Name, plugin.Name, plugin.Type.IsDefined(typeof(DefaultModuleAttribute)));

    [RelayCommand]
    public void DeleteAutoTarget(string item)
        => this.AutoTargets.Remove(item);

    [RelayCommand]
    public static void OpenThirdPartyLicenses()
    {
        var dir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "licenses");
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" \"{dir}\"") { CreateNoWindow = true });
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        var settings = new UserSettings()
        {
            Common = new()
            {
                ViewMode = this.ViewMode,
                IsEnableAutoTarget = this.IsEnableAutoTarget,
                OverlaySwitch = this.OverlaySwitch,
                IsEnableCaptureOverlay = this.IsEnableCaptureOverlay,
            },
            Targets = this.Targets.ToDictionary(t => t.Name, t => new TargetSettings()
            {
                Language = new()
                {
                    Source = t.Source,
                    Target = t.Target,
                },
                Font = t.Font,
                FontScale = t.FontScale,
                SelectedPlugins = new()
                {
                    [nameof(ITranslateModule)] = t.TranslateModule,
                    [nameof(ICacheModule)] = t.CacheModule,
                },
                PluginParams = t.Params.ToDictionary(p => p.GetType().Name),
            }),
        };
        Directory.CreateDirectory(PathUtility.UserDir);
        using var fs = File.Open(PathUtility.UserSettings, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(fs, settings, serializerOptions);
        this.autoTargetStore.AutoTargets.Clear();
        this.autoTargetStore.AutoTargets.UnionWith(this.AutoTargets);
        this.autoTargetStore.Save();
        await this.presentationService.CloseDialogAsync(true);
    }

    public void Dispose()
    {
        this.updateChecker.UpdateAvailable -= UpdateChecker_UpdateAvailable;
    }
}

record EnumItem<TEnum>(TEnum Value)
    where TEnum : Enum
{
    public string Display { get; } = typeof(TEnum).GetField(Value.ToString())?.GetCustomAttribute<LocalizedDescriptionAttribute>()?.Description ?? Value.ToString();
}

public record ModuleItem(string Name, string DisplayName, bool IsDefault);

public partial class TargetSettingsViewModel(
    string name,
    IServiceProvider sp,
    TargetSettings settings,
    IReadOnlyList<ModuleItem> translateModules,
    IReadOnlyList<ModuleItem> cacheModules)
    : ObservableObject
{
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
    public string Name { get; } = name;

    [Browsable(false)]
    public IEnumerable<ModuleItem> TranslateModules { get; } = translateModules;
    [Browsable(false)]
    public IEnumerable<ModuleItem> CacheModules { get; } = cacheModules;

    [Category("SettingsViewModel|Language")]
    [ItemsSourceProperty(nameof(Languages))]
    [SelectedValuePath(nameof(CultureInfo.Name))]
    [DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    public string Source { get; set; } = settings.Language.Source;

    [Category("SettingsViewModel|Language")]
    [ItemsSourceProperty(nameof(Languages))]
    [SelectedValuePath(nameof(CultureInfo.Name))]
    [DisplayMemberPath(nameof(CultureInfo.DisplayName))]
    public string Target { get; set; } = settings.Language.Target;

    [Category("SettingsViewModel|Plugin")]
    [ItemsSourceProperty(nameof(TranslateModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string TranslateModule { get; set; }
        = settings.SelectedPlugins.GetValueOrDefault(nameof(ITranslateModule), translateModules.OrderByDescending(i => i.IsDefault).First().Name);

    [Category("SettingsViewModel|Plugin")]
    [ItemsSourceProperty(nameof(CacheModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string CacheModule { get; set; }
        = settings.SelectedPlugins.GetValueOrDefault(nameof(ICacheModule), cacheModules.OrderByDescending(i => i.IsDefault).First().Name);

    [Category("SettingsViewModel|Font")]
    [FontFamilySelector]
    [FontPreview(18)]
    [SortIndex(5)]
    public string Font { get; set; } = settings.Font;

    [property: Category("SettingsViewModel|Font")]
    [property: Slidable(0.1, 5, 0.1, 1.0, true, 0.1)]
    [property: FormatString("F2")]
    [property: SortIndex(6)]
    [ObservableProperty]
    private double fontScale = settings.FontScale;

    public IReadOnlyList<IPluginParam> Params { get; } = sp.GetServices<IPluginParam>().Select(p =>
    {
        var configureType = typeof(IConfigureNamedOptions<>).MakeGenericType(p.GetType());
        var configures = (IEnumerable<object>)sp.GetService(typeof(IEnumerable<>).MakeGenericType(configureType))!;
        var configureMethod = configureType.GetMethod(nameof(IConfigureNamedOptions<object>.Configure))!;
        foreach (var configure in configures)
        {
            configureMethod.Invoke(configure, [name, p]);
        }
        return p;
    }).ToArray();
}
