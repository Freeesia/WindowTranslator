using Composition.WindowsRuntimeHelpers;
using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowTranslator;
using WindowTranslator.Modules.Cache;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.OverlayColor;
using WindowTranslator.Modules.Translate;
using WindowTranslator.Stores;

CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();


var builder = KamishibaiApplication<App, StartupDialog>.CreateBuilder();

#if !DEBUG
builder.Logging.AddSentry("https://5301a4e1d7cd4c45877493fd27303166@o351180.ingest.sentry.io/6471678");
#endif

builder.Host.ConfigureAppConfiguration((_, b) =>
{
    b.AddUserSecrets<Program>();
});


builder.Services.AddSingleton<IProcessInfoStore, ProcessInfoStore>();
builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
builder.Services.AddPresentation<MainWindow, MainViewModel>();
builder.Services.AddTransient<ICaptureModule, WindowsGraphicsCapture>();
builder.Services.AddTransient<IOcrModule, WindowsMediaOcr>();
builder.Services.Configure<DeepLOptions>(builder.Configuration.GetSection(nameof(DeepLOptions)));
//builder.Services.AddTransient<ITranslateModule, DeepLTranslator>();
builder.Services.AddTransient<ITranslateModule, TranslateEmptyModule>();
builder.Services.AddTransient<ICacheModule, LocalCache>();
builder.Services.AddTransient<IColorModule, ColorThiefModule>();

using var app = builder.Build();

await app.StartAsync();