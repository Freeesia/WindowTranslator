using Composition.WindowsRuntimeHelpers;
using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Reflection;
using Weikio.PluginFramework.Catalogs;
using WindowTranslator;
using WindowTranslator.Modules;
using WindowTranslator.Modules.Cache;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Stores;

CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();


var builder = KamishibaiApplication<App, StartupDialog>.CreateBuilder();
builder.Host.ConfigureAppConfiguration((_, b) =>
{
    b.AddUserSecrets<Program>();
});

builder.Services.AddPluginFramework()
    .AddPluginCatalog(new AssemblyPluginCatalog(Assembly.GetExecutingAssembly()))
    .AddPluginType<ITranslateModule>(configureDefault: op => op.DefaultType = GetPlugin<ITranslateModule, TranslateEmptyModule>)
    .AddPluginType<ICacheModule>(configureDefault: op => op.DefaultType = GetPlugin<ICacheModule, InMemoryCache>);

if (Directory.Exists(@".\plugins"))
{
    builder.Services.AddPluginCatalog(new FolderPluginCatalog(@".\plugins"));
}


builder.Services.AddSingleton<IProcessInfoStore, ProcessInfoStore>();
builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
builder.Services.AddPresentation<MainWindow, MainViewModel>();
builder.Services.AddTransient<ICaptureModule, WindowsGraphicsCapture>();
builder.Services.AddTransient<IOcrModule, WindowsMediaOcr>();
builder.Services.AddTransient<IColorModule, ColorThiefModule>();

builder.Services.AddTransient(typeof(IPluginOptions<>), typeof(PluginOptions<>));

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
