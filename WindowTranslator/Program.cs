using Composition.WindowsRuntimeHelpers;
using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PropertyTools.Wpf;
using System.IO;
using System.Reflection;
using Weikio.PluginFramework.Catalogs;
using WindowTranslator;
using WindowTranslator.Modules;
using WindowTranslator.Modules.Cache;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Main;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Modules.Settings;
using WindowTranslator.Modules.Startup;
using WindowTranslator.Stores;

CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();


var builder = KamishibaiApplication<App, StartupDialog>.CreateBuilder();
builder.Host.ConfigureAppConfiguration((_, b) =>
{
    b.AddUserSecrets<Program>();
});

builder.Services.AddPluginFramework()
    .AddPluginCatalog(new AssemblyPluginCatalog(Assembly.GetExecutingAssembly()))
    .AddPluginType<ITranslateModule>(configureDefault: op => op.DefaultType = GetPlugin<ITranslateModule, NoTranslateModule>)
    .AddPluginType<ICacheModule>(configureDefault: op => op.DefaultType = GetPlugin<ICacheModule, LocalCache>)
    .AddPluginType<IOcrModule>(configureDefault: op => op.DefaultType = GetPlugin<IOcrModule, WindowsMediaOcr>)
    .AddPluginType<ICaptureModule>(configureDefault: op => op.DefaultType = GetPlugin<ICaptureModule, WindowsGraphicsCapture>)
    .AddPluginType<IColorModule>(configureDefault: op => op.DefaultType = GetPlugin<IColorModule, ColorThiefModule>);

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
ViewTypeCache.SetViewType<PropertyDialog, SettingsViewModel>();
builder.Services.AddTransient(_ =>
{
    var dialog = new PropertyDialog();
    dialog.PropertyControl.SetCurrentValue(PropertyGrid.ControlFactoryProperty, new CustomControlFactory());
    return dialog;
});
builder.Services.AddTransient<SettingsViewModel>();
builder.Services.AddTransient(typeof(IPluginOptions<>), typeof(PluginOptions<>));
builder.Services.Configure<LanguageOptions>(builder.Configuration.GetSection(nameof(UserSettings.Language)));

using var app = builder.Build();

await app.StartAsync();

static Type GetPlugin<TInterface, TPlugin>(IServiceProvider serviceProvider, IEnumerable<Type> implementingTypes)
    where TPlugin : TInterface
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var dic = config.GetSection("SelectedPlugins").GetChildren().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    if (dic.TryGetValue(typeof(TInterface).Name, out var name))
    {
        return implementingTypes.FirstOrDefault(t => t.Name == name) ?? typeof(TPlugin);
    }
    return typeof(TPlugin);
}

public class TranslateEmptyModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(string[] srcTexts)
        => ValueTask.FromResult((string[])Array.CreateInstance(typeof(string), srcTexts.Length));
}

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
