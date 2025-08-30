using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using Microsoft.Extensions.Options;
using WindowTranslator.Properties;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Modules.ErrorReport;

[OpenDialog]
public partial class ErrorReportViewModel([Inject] IContentDialogService dialogService, [Inject] IOptionsSnapshot<UserSettings> options, string message, Exception ex, string target, string? lastIamgePath = null) : ObservableObject
{
    private readonly UserSettings settings = options.Value;
    private readonly IContentDialogService dialogService = dialogService;
    private readonly Exception ex = ex;
    private readonly string target = target;
    private readonly string? lastIamgePath = lastIamgePath;
    private bool sendFinished;

    [ObservableProperty]
    private bool copied = false;

    [ObservableProperty]
    private bool sent = false;

    public string Message { get; } = message;

    public string Info { get; } = GetInfo(ex, options.Value, target);

    public bool IsSentryEnabled { get; } = SentrySdk.IsEnabled;

    public bool HasImage => File.Exists(this.lastIamgePath);

    public string? ImagePath => this.HasImage ? this.lastIamgePath : null;

    [RelayCommand]
    private async Task CopyAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.CurrentCulture, $"Version: {AppInfo.Instance.Version}");
        sb.AppendLine(CultureInfo.CurrentCulture, $"Message: {this.Message}");
        sb.AppendLine(this.Info);

        Clipboard.SetText(sb.ToString());
        this.Copied = true;
        await Task.Delay(1000);
        this.Copied = false;
    }

    [RelayCommand]
    private async Task SendReportAsync()
    {
        if (this.sendFinished)
        {
            return;
        }
        var result = await this.dialogService.ShowSimpleDialogAsync(new()
        {
            Title = Resources.Confirm,
            Content = Resources.SendReportToolTip,
            CloseButtonText = Resources.Cancel,
            PrimaryButtonText = Resources.Submit,
        });
        if (result != ContentDialogResult.Primary)
        {
            return;
        }
        SentrySdk.CaptureException(this.ex, scope =>
        {
            if (File.Exists(this.lastIamgePath))
            {
                scope.AddAttachment(this.lastIamgePath);
            }
            if (settings.Targets.TryGetValue(target, out TargetSettings? setting))
            {
                setting.PluginParams.Clear();
                scope.Contexts["Target"] = new
                {
                    target,
                    setting,
                };
            }
        });
        await SentrySdk.FlushAsync();

        this.sendFinished = true;
        this.Sent = true;
        await Task.Delay(1000);
        this.Sent = false;
    }

    private static string GetInfo(Exception ex, UserSettings settings, string target)
    {
        if (ex == null)
            return string.Empty;

        var sb = new StringBuilder();

        sb.AppendLine("=== Exception Information ===");

        var currentException = ex;
        var depth = 0;

        while (currentException != null)
        {
            if (depth > 0)
            {
                sb.AppendLine();
                sb.AppendLine(CultureInfo.CurrentCulture, $"--- Inner Exception {depth} ---");
            }

            // 例外の型
            sb.AppendLine(CultureInfo.CurrentCulture, $"Type: {currentException.GetType().FullName}");

            // メッセージ
            if (!string.IsNullOrEmpty(currentException.Message))
            {
                sb.AppendLine(CultureInfo.CurrentCulture, $"Message: {currentException.Message}");
            }

            // スタックトレース
            if (!string.IsNullOrEmpty(currentException.StackTrace))
            {
                sb.AppendLine("StackTrace:");
                sb.AppendLine(currentException.StackTrace);
            }

            currentException = currentException.InnerException;
            depth++;
        }

        // TargetSettingsの情報を追加
        if (settings.Targets.TryGetValue(target, out TargetSettings? value))
        {
            sb.AppendLine();
            sb.AppendLine("=== Settings Information ===");
            sb.AppendLine(CultureInfo.CurrentCulture, $"Target: {target}");
            sb.AppendLine(CultureInfo.CurrentCulture, $"Language Source: {value.Language?.Source ?? "N/A"}");
            sb.AppendLine(CultureInfo.CurrentCulture, $"Language Target: {value.Language?.Target ?? "N/A"}");
            sb.AppendLine(CultureInfo.CurrentCulture, $"Font: {value.Font ?? "N/A"}");
            sb.AppendLine(CultureInfo.CurrentCulture, $"FontScale: {value.FontScale}");
            sb.AppendLine(CultureInfo.CurrentCulture, $"OverlayShortcut: {value.OverlayShortcut ?? "N/A"}");
            sb.AppendLine(CultureInfo.CurrentCulture, $"OverlayOpacity: {value.OverlayOpacity}");
            sb.AppendLine(CultureInfo.CurrentCulture, $"DisplayBusy: {value.DisplayBusy}");
            sb.AppendLine(CultureInfo.CurrentCulture, $"IsOneShotMode: {value.IsOneShotMode}");

            if (value.SelectedPlugins?.Count > 0)
            {
                sb.AppendLine("Selected Plugins:");
                foreach (var plugin in value.SelectedPlugins)
                {
                    sb.AppendLine(CultureInfo.CurrentCulture, $"  {plugin.Key}: {plugin.Value}");
                }
            }
        }

        return sb.ToString();
    }
}
