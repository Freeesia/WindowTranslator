using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PropertyTools.Wpf;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Weikio.PluginFramework.Abstractions;
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
using WindowTranslator.Stores;

//Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");
//Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("zh-CN");

var exeDir = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0])!;
Directory.SetCurrentDirectory(exeDir);

var builder = KamishibaiApplication<App, StartupDialog>.CreateBuilder();
builder.Services.AddPluginFramework()
    .AddPluginCatalog(new AssemblyPluginCatalog(Assembly.GetExecutingAssembly(), new() { PluginNameOptions = { PluginNameGenerator = GetPluginName } }))
    .AddPluginType<ITranslateModule>(configureDefault: op => op.DefaultType = GetPlugin<ITranslateModule>)
    .AddPluginType<ICacheModule>(configureDefault: op => op.DefaultType = GetPlugin<ICacheModule>)
    .AddPluginType<IOcrModule>(configureDefault: op => op.DefaultType = GetPlugin<IOcrModule>)
    .AddPluginType<ICaptureModule>(configureDefault: op => op.DefaultType = GetPlugin<ICaptureModule>)
    .AddPluginType<IColorModule>(configureDefault: op => op.DefaultType = GetPlugin<IColorModule>)
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
builder.Services.AddSingleton<ITargetStore, TargetStore>();
builder.Services.AddHostedService<WindowMonitor>();
builder.Services.AddScoped<IProcessInfoStore, ProcessInfoStore>();
builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
builder.Services.AddPresentation<CaptureMainWindow, CaptureMainViewModel>();
builder.Services.AddPresentation<OverlayMainWindow, OverlayMainViewModel>();
ViewTypeCache.SetViewType<PropertyDialog, SettingsViewModel>();
builder.Services.AddTransient(_ =>
{
    var dlg = new PropertyDialog();
    dlg.PropertyControl.SetCurrentValue(PropertyGrid.OperatorProperty, new SettingsPropertyGridOperator());
    dlg.PropertyControl.SetCurrentValue(PropertyGrid.ControlFactoryProperty, new SettingsPropertyGridFactory());
    return dlg;
});
builder.Services.AddTransient<SettingsViewModel>();
builder.Services.Configure<UserSettings>(builder.Configuration, op => op.ErrorOnUnknownConfiguration = false);
builder.Services.Configure<LanguageOptions>(builder.Configuration.GetSection(nameof(UserSettings.Language)));
builder.Services.AddTransient(typeof(IConfigureOptions<>), typeof(ConfigurePluginParam<>));

var app = builder.Build();

await app.RunAsync();

static Type GetPlugin<TInterface>(IServiceProvider serviceProvider, IEnumerable<Type> implementingTypes)
{
    var settings = serviceProvider.GetRequiredService<IOptionsSnapshot<UserSettings>>();
    var dic = settings.Value.SelectedPlugins;
    Type GetDefaultPlugin() => implementingTypes.OrderByDescending(t => t.IsDefined(typeof(DefaultModuleAttribute))).First();
    if (dic.TryGetValue(typeof(TInterface).Name, out var name))
    {
        return implementingTypes.FirstOrDefault(t => t.Name == name) ?? GetDefaultPlugin();
    }
    return GetDefaultPlugin();
}

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

[DisplayName("空文字化")]
public class TranslateEmptyModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(string[] srcTexts)
        => ValueTask.FromResult((string[])Array.CreateInstance(typeof(string), srcTexts.Length));
}

[DefaultModule]
[DisplayName("翻訳しない")]
public class NoTranslateModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(string[] srcTexts)
    => ValueTask.FromResult(srcTexts);
}

public class ConfigurePluginParam<TOptions>(IConfiguration config) : IConfigureOptions<TOptions>
    where TOptions : class, IPluginParam
{
    private readonly IConfiguration config = config.GetSection(nameof(UserSettings.PluginParams));

    public void Configure(TOptions options)
        => this.config.GetSection(typeof(TOptions).Name).Bind(options);
}
