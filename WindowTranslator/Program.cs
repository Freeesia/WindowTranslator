using Kamishibai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropertyTools.Wpf;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;

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
builder.Services.AddSingleton<UpdateChecker>()
    .AddSingleton<IUpdateChecker>(sp => sp.GetRequiredService<UpdateChecker>())
    .AddHostedService(sp => sp.GetRequiredService<UpdateChecker>());
builder.Services.AddScoped<IProcessInfoStoreInternal, ProcessInfoStore>()
    .AddScoped<IProcessInfoStore>(sp => sp.GetRequiredService<IProcessInfoStoreInternal>());
builder.Services.AddPresentation<StartupDialog, StartupViewModel>();
builder.Services.AddPresentation<CaptureMainWindow, CaptureMainViewModel>();
builder.Services.AddPresentation<OverlayMainWindow, OverlayMainViewModel>();
ViewTypeCache.SetViewType<PropertyDialog, SettingsViewModel>();
builder.Services.AddTransient(_ =>
{
    var dlg = new PropertyDialog();
    dlg.ShowInTaskbar = true;
    dlg.PropertyControl.SetCurrentValue(PropertyGrid.OperatorProperty, new SettingsPropertyGridOperator());
    dlg.PropertyControl.SetCurrentValue(PropertyGrid.ControlFactoryProperty, new SettingsPropertyGridFactory());
    dlg.SetResourceReference(FrameworkElement.StyleProperty, "DefaultWindowStyle");
    dlg.Resources.Remove(typeof(Button));
    dlg.SetCurrentValue(Window.WindowStyleProperty, WindowStyle.None);
    dlg.SetCurrentValue(Window.TitleProperty, string.Empty);
    dlg.SetCurrentValue(FrameworkElement.WidthProperty, 600d);
    dlg.SetCurrentValue(Window.SizeToContentProperty, SizeToContent.Height);
    var btnStyle = new Style(typeof(Button), (Style)Application.Current.FindResource(typeof(Button)));
    btnStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 120d));
    btnStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
    btnStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4)));
    btnStyle.Seal();
    dlg.Resources.Add(typeof(Button), btnStyle);
    var panel = (DockPanel)dlg.Content;
    var bar = new TitleBar() { ShowMinimize = false, ShowMaximize = false, Title = Resources.Settings };
    DockPanel.SetDock(bar, Dock.Top);
    panel.Children.Insert(0, bar);
    SystemThemeWatcher.Watch(dlg);
    dlg.Loaded += static (_, _) => ApplicationThemeManager.ApplySystemTheme(true);
    return dlg;
});
builder.Services.AddTransient<SettingsViewModel>();
builder.Services.Configure<UserSettings>(builder.Configuration, op => op.ErrorOnUnknownConfiguration = false);
builder.Services.Configure<LanguageOptions>(builder.Configuration.GetSection(nameof(UserSettings.Language)));
builder.Services.AddTransient(typeof(IConfigureOptions<>), typeof(ConfigurePluginParam<>));

var app = builder.Build();
using var mutex = new Mutex(false, "WindowTranslator", out var createdNew);
if (!createdNew)
{
    new MessageDialog()
    {
        Caption = "WindowTranslator",
        Icon = Kamishibai.MessageBoxImage.Error,
        Text = Resources.MutexError,
    }.Show();
    return;
}
await app.RunAsync();

static Type GetDefaultPlugin<TInterface>(IServiceProvider serviceProvider, IEnumerable<Type> implementingTypes)
    => implementingTypes.OrderByDescending(t => t.IsDefined(typeof(DefaultModuleAttribute))).First();

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

[DefaultModule]
[DisplayName("翻訳しない")]
public class NoTranslateModule : ITranslateModule
{
    public ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => ValueTask.FromResult(srcTexts.Select(s => s.Text).ToArray());
}

public class ConfigurePluginParam<TOptions>(IConfiguration config) : IConfigureOptions<TOptions>
    where TOptions : class, IPluginParam
{
    private readonly IConfiguration config = config.GetSection(nameof(UserSettings.PluginParams));

    public void Configure(TOptions options)
        => this.config.GetSection(typeof(TOptions).Name).Bind(options);
}

static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPluginType<T>(this IServiceCollection services, ServiceLifetime serviceLifetime = ServiceLifetime.Transient, Action<DefaultPluginOption> configureDefault = null) where T : class
    {
        var item = new ServiceDescriptor(typeof(IEnumerable<T>), (IServiceProvider sp) => sp.GetRequiredService<PluginProvider>().GetTypes<T>().AsEnumerable(), serviceLifetime);
        var item2 = new ServiceDescriptor(typeof(T), delegate (IServiceProvider sp)
        {
            var defaultPluginOptions = sp.GetDefaultPluginOptions<T>(configureDefault);
            var plugins = sp.GetRequiredService<PluginProvider>()
                .GetPlugins()
                .Where(p => typeof(T).IsAssignableFrom(p))
                .ToArray();
            var settings = sp.GetRequiredService<IOptionsSnapshot<UserSettings>>();
            var dic = settings.Value.SelectedPlugins;
            var plugin = dic.TryGetValue(typeof(T).Name, out var name)
                ? plugins.FirstOrDefault(p => p.Type.Name == name)
                : null;
            if (plugin is null)
            {
                var type = defaultPluginOptions.DefaultType(sp, plugins.Select(p => p.Type));
                plugin = plugins.FirstOrDefault(p => p.Type == type);
            }
            return plugin.Create<T>(sp);
        }, serviceLifetime);
        services.Add(item);
        services.Add(item2);
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
