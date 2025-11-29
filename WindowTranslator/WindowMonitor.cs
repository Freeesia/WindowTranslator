using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using WindowTranslator.Modules.Main;
using WindowTranslator.Stores;
using static Windows.Win32.PInvoke;

namespace WindowTranslator;
public class WindowMonitor(IMainWindowModule mainWindowModule, IAutoTargetStore autoTargetStore, IVirtualDesktopManager desktopManager, ILogger<WindowMonitor> logger) : BackgroundService
{
    private readonly IMainWindowModule mainWindowModule = mainWindowModule;
    private readonly IAutoTargetStore autoTargetStore = autoTargetStore;
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
            if (!IsWindowVisible(hWnd) || !this.desktopManager.IsWindowOnCurrentVirtualDesktop(hWnd) || this.checkedWindows.Contains(hWnd))
            {
                return true;
            }

            var windowTitle = GetWindowText(hWnd);
            if (GetWindowThreadProcessId(hWnd, out var processId) == 0)
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
            if (this.autoTargetStore.IsAutoTarget(hWnd, p.ProcessName))
            {
                this.logger.LogInformation($"`{p.ProcessName}`の翻訳を開始");
                this.checkedWindows.Add(hWnd);
                this.mainWindowModule.OpenTargetAsync(hWnd, p.ProcessName).Forget();
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
}
