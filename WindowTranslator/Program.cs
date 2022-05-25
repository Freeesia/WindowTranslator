using System;
using Composition.WindowsRuntimeHelpers;
using Kamishibai;
using Microsoft.Extensions.DependencyInjection;
using WindowTranslator;
using WindowTranslator.Modules.Cache;
using WindowTranslator.Modules.Capture;
using WindowTranslator.Modules.Ocr;
using WindowTranslator.Modules.Translate;

var builder = KamishibaiApplication<App, StartupDialog>.CreateBuilder();

CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread();

builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
builder.Services.AddPresentation<MainWindow, MainViewModel>();
builder.Services.AddTransient<ICaptureModule, WindowsGraphicsCapture>();
builder.Services.AddTransient<IOcrModule, WindowsMediaOcr>();
builder.Services.Configure<DeepLOptions>(builder.Configuration.GetSection(nameof(DeepLOptions)));
builder.Services.AddTransient<ITranslateModule, DeepLTranslator>();
builder.Services.AddTransient<ICacheModule, InMemoryCache>();

var app = builder.Build();

await app.StartAsync();