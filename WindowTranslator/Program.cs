using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
builder.Host.ConfigureAppConfiguration((_, b) =>
{
    b.AddUserSecrets<Program>();
});

builder.Services.AddPluginFramework()
    .AddPluginCatalog(new AssemblyPluginCatalog(Assembly.GetExecutingAssembly()))
    .AddPluginType<ITranslateModule>(configureDefault: op => op.DefaultType = GetPlugin<ITranslateModule>)
    .AddPluginType<ICacheModule>(configureDefault: op => op.DefaultType = GetPlugin<ICacheModule>)
    .AddPluginType<IOcrModule>(configureDefault: op => op.DefaultType = GetPlugin<IOcrModule>)
    .AddPluginType<ICaptureModule>(configureDefault: op => op.DefaultType = GetPlugin<ICaptureModule>)
    .AddPluginType<IColorModule>(configureDefault: op => op.DefaultType = GetPlugin<IColorModule>);

if (Directory.Exists(@".\plugins"))
{
    builder.Services.AddPluginCatalog(new FolderPluginCatalog(@".\plugins"));
}

var userPluginsDir = Path.Combine(PathUtility.UserDir, "plugins");
if (Directory.Exists(userPluginsDir))
{
    builder.Services.AddPluginCatalog(new FolderPluginCatalog(userPluginsDir));
}

builder.Configuration.AddJsonFile(PathUtility.UserSettings, true, true);

builder.Services.AddSingleton<IProcessInfoStore, ProcessInfoStore>();
builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
builder.Services.AddPresentation<MainWindow, MainViewModel>();
builder.Services.AddPresentation<PropertyDialog, SettingsViewModel>();
builder.Services.AddTransient(typeof(IPluginOptions<>), typeof(PluginOptions<>));
builder.Services.Configure<UserSettings>(builder.Configuration);

using var app = builder.Build();

await app.StartAsync();

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

public class PluginOptions<T> : IPluginOptions<T>
{
    private readonly IConfigurationSection config;
    private T? param;

    public T Param => this.param ??= this.config.Get<T>();

    public PluginOptions(IConfiguration configuration)
        => this.config = configuration.GetRequiredSection(typeof(T).Name);
}
