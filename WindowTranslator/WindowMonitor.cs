using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using PInvoke;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using Windows.UI.Notifications;
using WindowTranslator.Modules.Main;
using WindowTranslator.Stores;

namespace WindowTranslator;
public class WindowMonitor : BackgroundService
{
    private const string WindowHandle = "mainWindowHandle";
    private const string ProcessName = "processName";
    private readonly IMainWindowModule mainWindowModule;
    private readonly ITargetStore autoTargetStore;
    private readonly ILogger<WindowMonitor> logger;
    private readonly HashSet<IntPtr> checkedWindows = new();


    public WindowMonitor(IMainWindowModule mainWindowModule, ITargetStore autoTargetStore, ILogger<WindowMonitor> logger)
    {
        this.mainWindowModule = mainWindowModule;
        this.autoTargetStore = autoTargetStore;
        this.logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
        this.logger.LogInformation("通知監視");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        ToastNotificationManagerCompat.History.Clear();
        ToastNotificationManagerCompat.Uninstall();
        this.logger.LogInformation("通知削除");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            CheckProcesses();
            stoppingToken.ThrowIfCancellationRequested();
            await Task.Delay(5000, stoppingToken);
            stoppingToken.ThrowIfCancellationRequested();
        }
    }

    private void CheckProcesses()
    {
        this.logger.LogDebug("プロセスチェック開始");
        var windows = new HashSet<IntPtr>();
        User32.EnumWindows((hWnd, lParam) =>
        {
            if (!User32.IsWindowVisible(hWnd) || this.checkedWindows.Contains(hWnd))
            {
                return true;
            }

            var windowTitle = User32.GetWindowText(hWnd);
            if (User32.GetWindowThreadProcessId(hWnd, out var processId) == 0)
            {
                return true;
            }
            Process p;
            try
            {
                p = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return true;
            }
            if (this.autoTargetStore.IsTarget(hWnd, p.ProcessName))
            {
                ShowNotification(p, windowTitle, hWnd);
                this.checkedWindows.Add(hWnd);
            }
            else
            {
                windows.Add(hWnd);
            }
            return true;
        }, IntPtr.Zero);
        this.checkedWindows.ExceptWith(windows);
        this.logger.LogDebug("プロセスチェック終了");
    }
    private static void ShowNotification(Process process, string windowTitle, IntPtr windowHandle)
    {
        var builder = new ToastContentBuilder()
        .AddText("翻訳対象アプリが見つかりました")
            .AddText($"「{windowTitle}」を翻訳表示しますか？")
            .AddArgument(ProcessName, process.ProcessName)
            .AddArgument(WindowHandle, windowHandle.ToString(CultureInfo.InvariantCulture))
            .AddButton(new ToastButton()
                .SetContent("翻訳"))
            .AddButton(new ToastButton()
                .SetContent("キャンセル")
                .SetDismissActivation())
            .SetToastDuration(ToastDuration.Short);

        if (GetAppIcon(process) is { } path)
        {
            builder.AddAppLogoOverride(new Uri(path));
        }

        builder.Show(t =>
            {
                t.ExpiresOnReboot = true;
                t.ExpirationTime = DateTime.Now.AddSeconds(30);
                t.NotificationMirroring = NotificationMirroring.Disabled;
                t.Priority = ToastNotificationPriority.High;
            });
    }

    private static string? GetAppIcon(Process process)
    {
        var iconPath = Path.Combine(Path.GetTempPath(), "wt", $"{process.ProcessName}_icon.png");
        if (File.Exists(iconPath))
        {
            return iconPath;
        }
        if (GetProcessPath(process) is not { } exePath)
        {
            return null;
        }
        var icon = Icon.ExtractAssociatedIcon(exePath);
        if (icon is null)
        {
            return null;
        }
        // Save the icon to a temporary file
        Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
        var bmp = icon.ToBitmap();
        using var fs = File.OpenWrite(iconPath);
        bmp.Save(fs, ImageFormat.Png);
        return iconPath;
    }

    private async void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        var processName = args.Get(ProcessName);
        var mainWindowHandle = IntPtr.Parse(args.Get(WindowHandle), CultureInfo.InvariantCulture);
        this.logger.LogInformation("通知からのアタッチ");
        await this.mainWindowModule.OpenTargetAsync(mainWindowHandle, processName);
    }

    private static string? GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
