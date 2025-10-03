using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Win32;
using PropertyTools.DataAnnotations;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.AspNetCore;
using WindowTranslator.ComponentModel;
using WindowTranslator.Extensions;
using WindowTranslator.Modules.Main;
using WindowTranslator.Properties;
using WindowTranslator.Stores;
using Wpf.Ui;
using Wpf.Ui.Controls;
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
            new JsonStringEnumConverter(),
        },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };
    private readonly IUpdateChecker updateChecker;
    private readonly IContentDialogService dialogService;
    private readonly IPresentationService presentationService;
    private readonly IAutoTargetStore autoTargetStore;
    private readonly IEnumerable<ITargetSettingsValidator> validators;
    private readonly IMainWindowModule mainWindowModule;
    private readonly IConfigurationRoot? rootConfig;
    private readonly string target;
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
    private bool isOverlayPointSwap;

    [ObservableProperty]
    private bool isEnableAutoTarget;

    [ObservableProperty]
    private TargetSettingsViewModel selectedTarget;

    public IReadOnlyList<EnumItem<ViewMode>> ViewModes { get; } = Enum.GetValues<ViewMode>().Select(v => new EnumItem<ViewMode>(v)).ToArray();

    public IReadOnlyList<EnumItem<OverlaySwitch>> OverlaySwitches { get; } = Enum.GetValues<OverlaySwitch>().Select(v => new EnumItem<OverlaySwitch>(v)).ToArray();

    public bool IsCheckableCapture => this.ViewMode == ViewMode.Overlay;

    public ObservableCollection<string> AutoTargets { get; }

    public ObservableCollection<TargetSettingsViewModel> Targets { get; }

    public bool TargetMode => !string.IsNullOrEmpty(this.target);

    public bool ApplyMode { get; }

    public bool IsVisibleAbout { get; } = !AppInfo.SuppressMode;

    public AllSettingsViewModel(
        [Inject] PluginProvider provider,
        [Inject] IOptionsSnapshot<UserSettings> options,
        [Inject] IServiceProvider sp,
        [Inject] IUpdateChecker updateChecker,
        [Inject] IContentDialogService dialogService,
        [Inject] IPresentationService presentationService,
        [Inject] IAutoTargetStore autoTargetStore,
        [Inject] IConfiguration config,
        [Inject] IEnumerable<ITargetSettingsValidator> validators,
        [Inject] IMainWindowModule mainWindowModule,
        string target)
    {
        var items = provider.GetPlugins();
        var ocrModules = items.Where(p => typeof(IOcrModule).IsAssignableFrom(p.Type)).Select(Convert).ToArray();
        var translateModules = items.Where(p => typeof(ITranslateModule).IsAssignableFrom(p.Type)).Select(Convert).ToArray();
        var cacheModules = items.Where(p => typeof(ICacheModule).IsAssignableFrom(p.Type)).Select(Convert).ToArray();

        var common = options.Value.Common;
        this.ViewMode = common.ViewMode;
        this.IsEnableCaptureOverlay = common.IsEnableCaptureOverlay;
        this.OverlaySwitch = common.OverlaySwitch;
        this.IsOverlayPointSwap = common.IsOverlayPointSwap;
        this.IsEnableAutoTarget = common.IsEnableAutoTarget;
        this.AutoTargets = [.. autoTargetStore.AutoTargets];

        this.Targets = [.. options.Value.Targets
            .DefaultIfEmpty(new KeyValuePair<string, TargetSettings>(string.Empty, new()))
            .Select(t => new TargetSettingsViewModel(t.Key, sp, t.Value, ocrModules, translateModules, cacheModules))];

        this.ApplyMode = !string.IsNullOrEmpty(target) && options.Value.Targets.ContainsKey(target);
        if (this.Targets.FirstOrDefault(t => t.Name == target) is not { } selected)
        {
            selected = new(target, sp, options.Value.Targets.TryGetValue(string.Empty, out var d) ? d : new(), ocrModules, translateModules, cacheModules);
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
        this.validators = validators;
        this.mainWindowModule = mainWindowModule;
        this.target = target;
        this.rootConfig = config as IConfigurationRoot;
        this.updateChecker.UpdateAvailable += UpdateChecker_UpdateAvailable;
        SetUpUpdateInfo();
        this.isStartup = GetIsStartup();
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
    public void OpenReleaseNotes()
        => this.updateChecker.OpenChangelog();

    [RelayCommand]
    public Task CheckUpdateAsync(CancellationToken token)
        => this.updateChecker.CheckAsync(token);

    [RelayCommand]
    public void InstallUpdate()
        => this.updateChecker.Update();

    async partial void OnIsStartupChanged(bool value)
    {
        var exe = Assembly.GetExecutingAssembly();
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new InvalidOperationException();
        var name = exe.GetName().Name ?? throw new InvalidOperationException();
        var path = Environment.ProcessPath ?? throw new InvalidOperationException();
        if (value)
        {
            key.SetValue(name, path);
            await this.dialogService.ShowAlertAsync(Resources.AutoStart, string.Format(Resources.RegisterAutoStart, name), "OK");
        }
        else
        {
            key.DeleteValue(name, false);
            await this.dialogService.ShowAlertAsync(Resources.AutoStart, string.Format(Resources.UnregisterAutoStart, name), "OK");
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

    [RelayCommand(CanExecute = nameof(CanDeleteTargetSetting))]
    public void DeleteTargetSetting(TargetSettingsViewModel item)
        => this.Targets.Remove(item);

    public static bool CanDeleteTargetSetting(TargetSettingsViewModel item)
        => !string.IsNullOrEmpty(item?.Name);

    [RelayCommand]
    public async Task SaveAsync(object window)
    {
        using var b = EnterBusy();
        var settings = new UserSettings()
        {
            Common = new()
            {
                ViewMode = this.ViewMode,
                IsEnableAutoTarget = this.IsEnableAutoTarget,
                OverlaySwitch = this.OverlaySwitch,
                IsOverlayPointSwap = this.IsOverlayPointSwap,
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
                OverlayShortcut = t.OverlayShortcut,
                SelectedPlugins = new()
                {
                    [nameof(IOcrModule)] = t.OcrModule,
                    [nameof(ITranslateModule)] = t.TranslateModule,
                    [nameof(ICacheModule)] = t.CacheModule,
                },
                PluginParams = t.Params.ToDictionary(p => p.GetType().Name),
                DisplayBusy = t.DisplayBusy,
                IsOneShotMode = t.IsOneShotMode,
                OverlayOpacity = t.OverlayOpacity,
            }),
        };

        // 値の検証
        var results = new Dictionary<string, IReadOnlyList<ValidateResult>>();
        foreach (var (name, target) in string.IsNullOrEmpty(this.target) ? settings.Targets.ToArray() : [new KeyValuePair<string, TargetSettings>(this.target, settings.Targets[this.target])])
        {
            var r = new List<ValidateResult>();
            if (target.Language.Source == target.Language.Target)
            {
                r.Add(ValidateResult.Invalid(Resources.TranslateLanguage, Resources.SameSourceTargetLanguage));
            }

            if (!target.SelectedPlugins.TryGetValue(nameof(ITranslateModule), out var t) || string.IsNullOrEmpty(t))
            {
                r.Add(ValidateResult.Invalid(Resources.TranslateModule, """
                    翻訳モジュールが選択されていません。
                    「対象ごとの設定」→「全体設定」タブの「翻訳モジュール」を設定してください。
                    """));
            }
            if (!target.SelectedPlugins.TryGetValue(nameof(ICacheModule), out var c) || string.IsNullOrEmpty(c))
            {
                r.Add(ValidateResult.Invalid(Resources.CacheModule, """
                    キャッシュモジュールが選択されていません。
                    「対象ごとの設定」→「全体設定」タブの「キャッシュモジュール」を設定してください。
                    """));
            }
            foreach (var validator in this.validators)
            {
                r.Add(await validator.Validate(target));
            }
            if (r.Any(r => !r.IsValid))
            {
                results.Add(name, r.Where(r => !r.IsValid).ToArray());
            }
        }
        if (results.Any())
        {
            var r = await this.dialogService.ShowSimpleDialogAsync(new()
            {
                Title = Resources.SettingsInvalid,
                Content = string.Join("\n\n", results.Select(p => $"## {(p.Key is { Length: > 0 } n ? n : Resources.DefaultSetting)}\n{string.Join("\n", p.Value.Select(r => $"### {r.Title}\n{r.Message}"))}")),
                PrimaryButtonText = Resources.SaveAndClose,
                CloseButtonText = Resources.Cancel,
            });
            if (r != ContentDialogResult.Primary)
            {
                return;
            }
        }

        // 値の保存
        Directory.CreateDirectory(PathUtility.UserDir);
        using (var fs = File.Open(PathUtility.UserSettings, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(fs, settings, serializerOptions);
        this.autoTargetStore.AutoTargets.Clear();
        this.autoTargetStore.AutoTargets.UnionWith(this.AutoTargets);
        this.autoTargetStore.Save();
        this.rootConfig?.Reload();
        if (this.ApplyMode)
        {
            foreach (var (_, handle, w) in this.mainWindowModule.OpenedWindows.Where(w => w.Name == target).ToArray())
            {
                await w.CloseAsync();
                await this.mainWindowModule.OpenTargetAsync(handle, target);
            }
        }
        else
        {
            await this.presentationService.CloseDialogAsync(true, window);
        }
    }

    private DisposeAction EnterBusy()
    {
        this.IsBusy = true;
        return new DisposeAction(() => this.IsBusy = false);
    }

    public void Dispose()
    {
        this.updateChecker.UpdateAvailable -= UpdateChecker_UpdateAvailable;
    }
}

record EnumItem<TEnum>(TEnum Value)
    where TEnum : Enum
{
    public string Display { get; } = typeof(TEnum).GetField(Value.ToString())?.GetCustomAttribute<LocalizedDescriptionAttribute>()?.Description
        ?? typeof(TEnum).GetResourceManager()?.GetString(Value.ToString(), CultureInfo.CurrentCulture)
        ?? Value.ToString();
}

public record ModuleItem(string Name, string DisplayName, bool IsDefault);

public partial class TargetSettingsViewModel(
    string name,
    IServiceProvider sp,
    TargetSettings settings,
    IReadOnlyList<ModuleItem> ocrModules,
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
        CultureInfo.GetCultureInfo("zh-Hans"),
        CultureInfo.GetCultureInfo("zh-Hant"),
        CultureInfo.GetCultureInfo("vi-VN"),
    ];

    [Browsable(false)]
    public string Name { get; } = name;

    [Browsable(false)]
    public IEnumerable<ModuleItem> OcrModules { get; } = ocrModules;
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
    [ItemsSourceProperty(nameof(OcrModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string OcrModule { get; set; }
        = settings.SelectedPlugins.GetValueOrDefault(
            nameof(IOcrModule),
            ocrModules.OrderByDescending(i => i.IsDefault).FirstOrDefault()?.Name ?? string.Empty);

    [Category("SettingsViewModel|Plugin")]
    [ItemsSourceProperty(nameof(TranslateModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string TranslateModule { get; set; }
        = settings.SelectedPlugins.GetValueOrDefault(
            nameof(ITranslateModule),
            translateModules.OrderByDescending(i => i.IsDefault).FirstOrDefault()?.Name ?? string.Empty);

    [Category("SettingsViewModel|Plugin")]
    [ItemsSourceProperty(nameof(CacheModules))]
    [SelectedValuePath(nameof(ModuleItem.Name))]
    [DisplayMemberPath(nameof(ModuleItem.DisplayName))]
    public string CacheModule { get; set; }
        = settings.SelectedPlugins.GetValueOrDefault(
            nameof(ICacheModule),
            cacheModules.OrderByDescending(i => i.IsDefault).FirstOrDefault()?.Name ?? string.Empty);

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

    [property: Category("SettingsViewModel|Shortcut")]
    [ObservableProperty]
    private string overlayShortcut = settings.OverlayShortcut;

    [property: Category("SettingsViewModel|Misc")]
    [property: SortIndex(7)]
    [property: Slidable(0, 1, 0.005, 0.05, true, 0.01)]
    [property: FormatString("P1")]
    [ObservableProperty]
    private double overlayOpacity = settings.OverlayOpacity;

    [property: Category("SettingsViewModel|Misc")]
    [property: SortIndex(8)]
    [ObservableProperty]
    private bool displayBusy = settings.DisplayBusy;

    [property: Category("SettingsViewModel|Misc")]
    [property: SortIndex(9)]
    [ObservableProperty]
    private bool isOneShotMode = settings.IsOneShotMode;

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
