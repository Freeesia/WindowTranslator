using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using WindowTranslator.Extensions;
using WindowTranslator.Modules.Main;
using static Windows.Win32.PInvoke;

namespace WindowTranslator;

public class WindowMonitor(IMainWindowModule mainWindowModule, IOptionsMonitor<UserSettings> userSettings, IVirtualDesktopManager desktopManager, ILogger<WindowMonitor> logger) : BackgroundService
{
    private readonly IMainWindowModule mainWindowModule = mainWindowModule;
    private readonly IOptionsMonitor<UserSettings> userSettings = userSettings;
    private readonly IVirtualDesktopManager desktopManager = desktopManager;
    private readonly ILogger<WindowMonitor> logger = logger;
    private readonly HashSet<IntPtr> checkedWindows = [];

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
        EnumWindows((hWnd, lParam) =>
        {
            if (hWnd.ShouldIgnore() || !this.desktopManager.IsWindowOnCurrentVirtualDesktop(hWnd))
            {
                return true;
            }
            windows.Add(hWnd);
            if (this.checkedWindows.Contains(hWnd))
            {
                return true;
            }

            if (!hWnd.TryGetProcessId(out var processId))
            {
                return true;
            }
            Process p;
            try
            {
                p = Process.GetProcessById(unchecked((int)processId));
            }
            catch (ArgumentException)
            {
                return true;
            }
            if (ShouldAutoAttach(this.userSettings.CurrentValue, p.ProcessName)
                && !this.mainWindowModule.IsTargetOpened(hWnd))
            {
                this.logger.LogInformation($"`{p.ProcessName}`の翻訳を開始");
                this.checkedWindows.Add(hWnd);
                this.mainWindowModule.OpenTargetAsync(hWnd, p.ProcessName).Forget();
            }
            return true;
        }, IntPtr.Zero);
        this.checkedWindows.IntersectWith(windows);
        this.logger.LogDebug("プロセスチェック終了");
    }

    internal static bool ShouldAutoAttach(UserSettings settings, string processName)
        => settings.Targets.TryGetValue(processName, out var target)
            && target.IsEnableAutoTarget;
}
