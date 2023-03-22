using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Windows.Media;
using Windows.UI.Notifications;
using WindowTranslator.Modules.Main;

namespace WindowTranslator;
public class ProcessMonitor : IHostedService
{
    private readonly IMainWindowModule mainWindowModule;
    private readonly IOptionsMonitor<UserSettings> options;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly List<int> processIds = new();


    public ProcessMonitor(IMainWindowModule mainWindowModule, IOptionsMonitor<UserSettings> options)
    {
        this.mainWindowModule = mainWindowModule;
        this.options = options;
        this.cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Run(ChckLoopAsync, this.cancellationTokenSource.Token);
    }

    public async Task ChckLoopAsync()
    {
        while (!this.cancellationTokenSource.Token.IsCancellationRequested)
        {
            var settins = options.CurrentValue;
            if (settins.IsEnableAutoTarget)
            {
                await CheckProcessesAsync(settins.AutoTargets.ToHashSet());
            }
            this.cancellationTokenSource.Token.ThrowIfCancellationRequested();
            await Task.Delay(5000);
            this.cancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    private async Task CheckProcessesAsync(HashSet<string> targets)
    {
        foreach (var process in Process.GetProcesses())
        {
            this.cancellationTokenSource.Token.ThrowIfCancellationRequested();
            if (targets.Contains(process.ProcessName) && process.MainWindowHandle != IntPtr.Zero)
            {
                try
                {
                    await this.mainWindowModule.OpenTargetAsync(process.MainWindowHandle, process.ProcessName);
                }
                catch (Exception e)
                {
                    ShowNotification(
                        "WindowTranslator", $"""
                        キャプチャに失敗しました。
                        エラー：{e.Message}
                        """);
                }
                this.processIds.Add(process.Id);
                this.cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
        }
    }

    private static void ShowNotification(string title, string message)
    {
        var visual = new ToastVisual
        {
            BindingGeneric = new ToastBindingGeneric
            {
                Children =
                    {
                        new AdaptiveText { Text = title },
                        new AdaptiveText { Text = message }
                    }
            }
        };

        var content = new ToastContent { Visual = visual };
        var toast = new ToastNotification(content.GetXml());
        ToastNotificationManager.CreateToastNotifier()
            .Show(toast);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.cancellationTokenSource.Cancel(); ;
        return Task.CompletedTask;
    }
}
