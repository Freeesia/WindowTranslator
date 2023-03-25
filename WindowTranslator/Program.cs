using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PropertyTools.Wpf;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Weikio.PluginFramework.Catalogs;
using WindowTranslator;
using WindowTranslator.Modules;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Modules.Settings;
using WindowTranslator.Modules.Startup;
using WindowTranslator.Stores;

var builder = KamishibaiApplication<App, StartupDialog>.CreateBuilder();
builder.Services.AddPluginFramework()
    .AddPluginCatalog(new AssemblyPluginCatalog(Assembly.GetExecutingAssembly()))
    .AddPluginType<ITranslateModule>(configureDefault: op => op.DefaultType = GetPlugin<ITranslateModule>)
    .AddPluginType<ICacheModule>(configureDefault: op => op.DefaultType = GetPlugin<ICacheModule>)
    .AddPluginType<IOcrModule>(configureDefault: op => op.DefaultType = GetPlugin<IOcrModule>)
    .AddPluginType<ICaptureModule>(configureDefault: op => op.DefaultType = GetPlugin<ICaptureModule>)
    .AddPluginType<IColorModule>(configureDefault: op => op.DefaultType = GetPlugin<IColorModule>)
    .AddPluginType<IPluginParam>();

if (Directory.Exists(@".\plugins"))
{
    builder.Services.AddPluginCatalog(new FolderPluginCatalog(@".\plugins"));
}

var userPluginsDir = Path.Combine(PathUtility.UserDir, "plugins");
if (Directory.Exists(userPluginsDir))
{
    builder.Services.AddPluginCatalog(new FolderPluginCatalog(userPluginsDir));
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

await builder.Build().RunAsync();

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

public class ConfigurePluginParam<TOptions> : IConfigureOptions<TOptions>
    where TOptions : class, IPluginParam
{
    private readonly IConfiguration config;

    public ConfigurePluginParam(IConfiguration config)
        => this.config = config.GetSection(nameof(UserSettings.PluginParams));

    public void Configure(TOptions options)
        => this.config.GetSection(typeof(TOptions).Name).Bind(options);
}
