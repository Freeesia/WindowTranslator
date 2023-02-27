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
    .AddPluginType<ITranslateModule>(configureDefault: op => op.DefaultType = (_, _) => typeof(TranslateEmptyModule))
    .AddPluginType<ICacheModule>(configureDefault: op => op.DefaultType = (_, _) => typeof(InMemoryCache));

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

using var app = builder.Build();

await app.StartAsync();


public class TranslateEmptyModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(string[] srcTexts)
        => ValueTask.FromResult(srcTexts);
}
