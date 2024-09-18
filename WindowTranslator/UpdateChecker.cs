using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using Octokit;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Windows.UI.Notifications;

namespace WindowTranslator;

internal class UpdateChecker : BackgroundService, IUpdateChecker
{
    private const string owner = "Freeesia";
    private static readonly string updateInfoPath = Path.Combine(PathUtility.UserDir, "update.json");
    private readonly GitHubClient client;
    private readonly string name;
    private readonly Version version;
    private readonly ILogger<UpdateChecker> logger;
    private readonly App app;

    private bool hasUpdate;

    public event EventHandler? UpdateAvailable;

    public bool HasUpdate
    {
        get => this.hasUpdate;
        private set
        {
            if (this.hasUpdate != value)
            {
                this.hasUpdate = value;
                this.UpdateAvailable?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string? LatestVersion { get; private set; }

    public UpdateChecker(ILogger<UpdateChecker> logger, App app)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetName();
        this.name = name.Name ?? throw new InvalidOperationException();
        this.version = name.Version ?? throw new InvalidOperationException();
        this.client = new(new ProductHeaderValue(this.name, this.version.ToString()));
        this.logger = logger;
        this.app = app;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsInstalled())
        {
            this.logger.LogInformation("インストールされていないアプリなのでチェックしない");
            return;
        }
        await this.app.WaitForStartupAsync();
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAndDownloadAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
        finally
        {
            ToastNotificationManagerCompat.History.Clear();
            ToastNotificationManagerCompat.Uninstall();
        }
    }

    private async ValueTask CheckAndDownloadAsync(CancellationToken stoppingToken)
    {
        var updateInfo = await LoadUpdateInfoAsync().ConfigureAwait(false);

        // 更新情報がない場合は最新のリリースを取得
        // 1日以上経過していたら最新のリリースを取得
        if (updateInfo is null || updateInfo.CheckedAt < DateTime.UtcNow.AddDays(-1))
        {
            var release = await this.client.Repository.Release.GetLatest(owner, this.name);
            stoppingToken.ThrowIfCancellationRequested();
            var version = release.Name.TrimStart('v');

            if (new Version(version) <= this.version)
            {
                this.logger.LogInformation("アプリケーションは最新のバージョンです。");
                await SaveUpdateInfoAsync(new(version, release.HtmlUrl, null, DateTime.UtcNow, false)).ConfigureAwait(false);
                return;
            }
            this.logger.LogInformation($"新しいバージョン {version} が利用可能です。");
            var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
            if (asset is null)
            {
                this.logger.LogWarning("インストーラーが見つかりませんでした。");
                return;
            }
            string installerUrl = asset.BrowserDownloadUrl;

            // インストーラーをダウンロードして実行
            var dir = Path.Combine(Path.GetTempPath(), this.name);
            string installerPath = Path.Combine(dir, asset.Name);
            if (File.Exists(installerPath))
            {
                this.logger.LogInformation("インストーラーはすでにダウンロードされています。");
            }
            else
            {
                Directory.CreateDirectory(dir);
                using var downloader = new HttpClient();
                using var fs = File.Create(installerPath);
                using var stream = await downloader.GetStreamAsync(installerUrl, stoppingToken);
                await stream.CopyToAsync(fs, stoppingToken);
                this.logger.LogInformation("インストーラーをダウンロードしました。");
            }
            await SaveUpdateInfoAsync(new(version, release.HtmlUrl, installerPath, DateTime.UtcNow, false)).ConfigureAwait(false);
            ShowUpdateNotification(version, release.HtmlUrl, installerPath, false);
            this.LatestVersion = version;
            this.HasUpdate = true;
        }
        // バージョンが新しい場合は通知
        else if (new Version(updateInfo.Version) > this.version && !updateInfo.Skip && updateInfo.Path is not null && File.Exists(updateInfo.Path))
        {
            ShowUpdateNotification(updateInfo.Version, updateInfo.Url, updateInfo.Path, false);
            this.LatestVersion = updateInfo.Version;
            this.HasUpdate = true;
        }
    }

    private static void ShowUpdateNotification(string version, string url, string path, bool supress)
    {
        var builder = new ToastContentBuilder()
            .AddText($"新しいバージョン {version} がリリースされました", AdaptiveTextStyle.Title)
            .AddText($"更新版をインストールしますか？")
            .AddArgument(nameof(UpdateChecker))
            .AddArgument(nameof(url), url)
            .AddArgument(nameof(path), path)
            .AddArgument(nameof(version), version)
            .AddButton(new ToastButton()
                .AddArgument("action", ToastActions.Install)
                .SetContent("インストール"))
            .AddButton(new ToastButton()
                .SetContent("更新内容の確認")
                .AddArgument("action", ToastActions.OpenBrowser)
                .SetBackgroundActivation());

        {
            var args = ToastArguments.Parse(builder.Content.Launch);
            args.Add("action", ToastActions.Skip);
            builder.Content.Actions.ContextMenuItems.Add(new("このバージョンをスキップ", args.ToString()));
        }

        builder.Show(t =>
        {
            t.ExpiresOnReboot = true;
            t.NotificationMirroring = NotificationMirroring.Disabled;
            t.SuppressPopup = supress;
        });
    }

    private async void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        if (!args.Contains(nameof(UpdateChecker)))
        {
            return;
        }
        if (!args.TryGetValue<ToastActions>("action", out var action))
        {
            this.app.Dispatcher.Invoke(this.app.MainWindow.Show);
            return;
        }
        switch (action)
        {
            case ToastActions.Install:
                Process.Start("msiexec", $"/i {args.Get("path")}");
                break;
            case ToastActions.Skip:
                await SaveUpdateInfoAsync(new(args.Get("version"), args.Get("url"), args.Get("path"), DateTime.UtcNow, true)).ConfigureAwait(false);
                break;
            case ToastActions.OpenBrowser:
                Process.Start(new ProcessStartInfo(args.Get("url")) { UseShellExecute = true });
                ShowUpdateNotification(args.Get("version"), args.Get("url"), args.Get("path"), true);
                break;
            default:
                break;
        }
    }

    public async Task CheckAsync(CancellationToken token)
    {
        var updateInfo = await LoadUpdateInfoAsync();
        if (updateInfo is null)
        {
            await CheckAndDownloadAsync(token).ConfigureAwait(false);
        }
        else if (new Version(updateInfo.Version) > this.version && !updateInfo.Skip && updateInfo.Path is not null && File.Exists(updateInfo.Path))
        {
            ShowUpdateNotification(updateInfo.Version, updateInfo.Url, updateInfo.Path, false);
            this.LatestVersion = updateInfo.Version;
            this.HasUpdate = true;
        }
        else
        {
            await SaveUpdateInfoAsync(updateInfo with { CheckedAt = DateTime.MinValue, Skip = false }).ConfigureAwait(false);
            await CheckAndDownloadAsync(token).ConfigureAwait(false);
        }
    }

    public async void Update()
    {
        var updateInfo = await LoadUpdateInfoAsync().ConfigureAwait(false);
        if (updateInfo is not null && updateInfo.Path is not null && File.Exists(updateInfo.Path))
        {
            Process.Start("msiexec", $"/i {updateInfo.Path}");
        }
    }

    public async void OpenChangelog()
    {
        var updateInfo = await LoadUpdateInfoAsync().ConfigureAwait(false);
        if (updateInfo is not null)
        {
            Process.Start(new ProcessStartInfo(updateInfo.Url) { UseShellExecute = true });
        }
    }

    private enum ToastActions
    {
        Install,
        Skip,
        OpenBrowser
    }

    private async ValueTask<UpdateInfo?> LoadUpdateInfoAsync()
    {
        try
        {
            if (File.Exists(updateInfoPath))
            {
                using var fs = File.OpenRead(updateInfoPath);
                return await JsonSerializer.DeserializeAsync<UpdateInfo>(fs).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "更新情報の読み込みに失敗しました");
        }
        return null;
    }

    private async ValueTask SaveUpdateInfoAsync(UpdateInfo updateInfo)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(updateInfoPath)!);
            using var fs = File.Create(updateInfoPath);
            await JsonSerializer.SerializeAsync(fs, updateInfo).ConfigureAwait(false);
        }
        catch (Exception)
        {
            this.logger.LogError("更新情報の保存に失敗しました");
        }
    }

    private static bool IsInstalled()
    {
        if (GetInstallDir() is not { } path)
        {
            return false;
        }
        return Path.GetDirectoryName(Environment.ProcessPath) == path;
    }

    private static string? GetInstallDir()
    {
        string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{1D495A96-C8B4-4314-A08B-60665057B447}";
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default).OpenSubKey(registryKey);
        return key?.GetValue("InstallLocation") as string;
    }
}

interface IUpdateChecker
{
    bool HasUpdate { get; }
    string? LatestVersion { get; }

    event EventHandler? UpdateAvailable;

    Task CheckAsync(CancellationToken token = default);
    void Update();
    void OpenChangelog();
}

record UpdateInfo(string Version, string Url, string? Path, DateTime CheckedAt, bool Skip);