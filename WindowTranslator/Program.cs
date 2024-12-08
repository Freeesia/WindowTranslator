using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Weikio.PluginFramework.Abstractions;
using Weikio.PluginFramework.AspNetCore;
using Weikio.PluginFramework.Catalogs;
using WindowTranslator;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Modules.Settings;
using WindowTranslator.Modules.Startup;
using WindowTranslator.Properties;
using WindowTranslator.Stores;
using Wpf.Ui;

//Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
//Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");

var exeDir = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0])!;
Directory.SetCurrentDirectory(exeDir);

var builder = KamishibaiApplication<App, StartupDialog>.CreateBuilder();

#if !DEBUG
// Sentryを無効化するために空のDSNを設定する必要があるけど、環境変数からは空文字を設定できないので、ここで設定する
// 環境変数でDNSが設定されているときはそちらが優先されるはず
builder.Host.ConfigureLogging((c, l) => l.AddConfiguration(c.Configuration).AddSentry(op => op.Dsn = ""));
#endif


builder.Services.AddPluginFramework()
    .AddPluginCatalog(new AssemblyPluginCatalog(Assembly.GetExecutingAssembly(), new() { PluginNameOptions = { PluginNameGenerator = GetPluginName } }))
    .AddPluginType<ITranslateModule>(ServiceLifetime.Scoped, op => op.DefaultType = GetDefaultPlugin<ITranslateModule>)
    .AddPluginType<ICacheModule>(ServiceLifetime.Scoped, op => op.DefaultType = GetDefaultPlugin<ICacheModule>)
    .AddPluginType<IOcrModule>(ServiceLifetime.Scoped, op => op.DefaultType = GetDefaultPlugin<IOcrModule>)
    .AddPluginType<ICaptureModule>(ServiceLifetime.Scoped, op => op.DefaultType = GetDefaultPlugin<ICaptureModule>)
    .AddPluginType<IColorModule>(ServiceLifetime.Scoped, op => op.DefaultType = GetDefaultPlugin<IColorModule>)
    .AddPluginType<IFilterModule>(ServiceLifetime.Scoped)
    .AddPluginType<ITargetSettingsValidator>()
    .AddPluginType<IPluginParam>();

var appPluginDir = @".\plugins";
if (Directory.Exists(appPluginDir))
{
    builder.Services.AddPluginCatalog(new FolderPluginCatalog(appPluginDir, new FolderPluginCatalogOptions() { PluginNameOptions = { PluginNameGenerator = GetPluginName } }));
}

var userPluginsDir = Path.Combine(PathUtility.UserDir, "plugins");
if (Directory.Exists(userPluginsDir))
{
    builder.Services.AddPluginCatalog(new FolderPluginCatalog(userPluginsDir, new FolderPluginCatalogOptions() { PluginNameOptions = { PluginNameGenerator = GetPluginName } }));
}

builder.Configuration
    .AddCommandLine(args)
    .AddJsonFile(PathUtility.UserSettings, true, true);

builder.Services.AddSingleton<IMainWindowModule, MainWindowModule>();
builder.Services.AddSingleton<IAutoTargetStore, AutoTargetStore>();
builder.Services.AddHostedService<WindowMonitor>();
builder.Services.AddSingleton<UpdateChecker>()
    .AddSingleton<IUpdateChecker>(sp => sp.GetRequiredService<UpdateChecker>())
    .AddHostedService(sp => sp.GetRequiredService<UpdateChecker>());
builder.Services.AddScoped<IProcessInfoStoreInternal, ProcessInfoStore>()
    .AddScoped<IProcessInfoStore>(sp => sp.GetRequiredService<IProcessInfoStoreInternal>());
builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
builder.Services.AddPresentation<CaptureMainWindow, CaptureMainViewModel>();
builder.Services.AddPresentation<OverlayMainWindow, OverlayMainViewModel>();
builder.Services.AddPresentation<AllSettingsDialog, AllSettingsViewModel>();
builder.Services.AddSingleton<IContentDialogService, ContentDialogService>();
builder.Services.Configure<UserSettings>(builder.Configuration, op => op.ErrorOnUnknownConfiguration = false);
builder.Services.Configure<CommonSettings>(builder.Configuration.GetSection(nameof(UserSettings.Common)));
builder.Services.AddTransient(typeof(IConfigureNamedOptions<>), typeof(ConfigurePluginParam<>));
builder.Services.AddTransient(typeof(IConfigureOptions<>), typeof(ConfigurePluginParam<>));
builder.Services.AddTransient<IConfigureOptions<TargetSettings>, ConfigureTargetSettings>();
builder.Services.AddTransient<IConfigureOptions<LanguageOptions>, ConfigureLanguageOptions>();
builder.Services.AddSingleton(_ => (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a"))!)!);

var app = builder.Build();
#if DEBUG
var createdNew = false;
#else
using var mutex = new Mutex(false, "WindowTranslator", out var createdNew);
#endif
if (!createdNew)
{
    new MessageDialog()
    {
        Caption = "WindowTranslator",
        Icon = MessageBoxImage.Error,
        Text = Resources.MutexError,
    }.Show();
    return;
}
await app.RunAsync();

static Type? GetDefaultPlugin<TInterface>(IServiceProvider serviceProvider, IEnumerable<Type> implementingTypes)
    => implementingTypes.OrderByDescending(t => t.IsDefined(typeof(DefaultModuleAttribute)))
        .FirstOrDefault();

static string GetPluginName(PluginNameOptions options, Type type)
{
    if (type.GetCustomAttribute<LocalizedDisplayNameAttribute>() is { } ldattr)
    {
        return ldattr.DisplayName;
    }
    else if (type.GetCustomAttribute<DisplayNameAttribute>() is { } dattr)
    {
        return dattr.DisplayName;
    }
    else
    {
        return type.Name;
    }
}

class ConfigurePluginParam<TOptions>(IConfiguration configuration, IProcessInfoStore store) : IConfigureNamedOptions<TOptions>
    where TOptions : class, IPluginParam
{
    private readonly IConfiguration configuration = configuration.GetSection(nameof(UserSettings.Targets));
    private readonly IProcessInfoStore store = store;

    public void Configure(TOptions options)
    {
        var section = this.configuration.GetSection(this.store.Name);
        if (!section.Exists())
        {
            section = this.configuration.GetSection(Options.DefaultName);
        }
        section
            .GetSection(nameof(TargetSettings.PluginParams))
            .GetSection(typeof(TOptions).Name)
            .Bind(options);
    }

    public void Configure(string? name, TOptions options)
    {
        name = (string.IsNullOrEmpty(name) ? this.store.Name : name) ?? string.Empty;
        var section = this.configuration.GetSection(name);
        if (!section.Exists())
        {
            section = this.configuration.GetSection(Options.DefaultName);
        }
        section
            .GetSection(nameof(TargetSettings.PluginParams))
            .GetSection(typeof(TOptions).Name)
            .Bind(options);
    }
}

class ConfigureTargetSettings(IConfiguration configuration, IProcessInfoStore store) : IConfigureOptions<TargetSettings>
{
    private readonly IConfiguration configuration = configuration.GetSection(nameof(UserSettings.Targets));
    private readonly IProcessInfoStore store = store;

    public void Configure(TargetSettings options)
    {
        var section = this.configuration.GetSection(this.store.Name);
        if (!section.Exists())
        {
            section = this.configuration.GetSection(Options.DefaultName);
        }
        section.Bind(options);
    }
}


class ConfigureLanguageOptions(IConfiguration configuration, IProcessInfoStore store) : IConfigureOptions<LanguageOptions>
{
    private readonly IConfiguration configuration = configuration.GetSection(nameof(UserSettings.Targets));
    private readonly IProcessInfoStore store = store;

    public void Configure(LanguageOptions options)
    {
        var section = this.configuration.GetSection(this.store.Name);
        if (!section.Exists())
        {
            section = this.configuration.GetSection(Options.DefaultName);
        }
        section.GetSection(nameof(TargetSettings.Language)).Bind(options);
    }
}

static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPluginType<T>(this IServiceCollection services, ServiceLifetime serviceLifetime = ServiceLifetime.Transient, Action<DefaultPluginOption>? configureDefault = null) where T : class
    {
        services.Add(new(typeof(IEnumerable<T>), sp => sp.GetRequiredService<PluginProvider>().GetTypes<T>(), serviceLifetime));
        services.Add(new(typeof(T), sp =>
        {
            var defaultPluginOptions = sp.GetDefaultPluginOptions<T>(configureDefault);
            var plugins = sp.GetRequiredService<PluginProvider>()
                .GetPlugins()
                .Where(p => typeof(T).IsAssignableFrom(p))
                .ToArray();
            var settings = sp.GetRequiredService<IOptionsSnapshot<TargetSettings>>();
            var dic = settings.Value.SelectedPlugins;
            var plugin = dic.TryGetValue(typeof(T).Name, out var name)
                ? plugins.FirstOrDefault(p => p.Type.Name == name)
                : null;
            if (plugin is null)
            {
                var type = defaultPluginOptions.DefaultType(sp, plugins.Select(p => p.Type));
                plugin = plugins.FirstOrDefault(p => p.Type == type);
            }
#pragma warning disable CS8603 // null返すことを許容する
            return plugin?.Create<T>(sp);
#pragma warning restore CS8603
        }, serviceLifetime));
        return services;
    }

    private static DefaultPluginOption GetDefaultPluginOptions<T>(this IServiceProvider sp, Action<DefaultPluginOption>? configureDefault)
        where T : class
    {
        var defaultPluginOption = sp.GetService<IOptionsMonitor<DefaultPluginOption>>()?.Get(typeof(T).Name) ?? new();
        configureDefault?.Invoke(defaultPluginOption);
        return defaultPluginOption;
    }
}
