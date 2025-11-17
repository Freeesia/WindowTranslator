using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace WindowTranslator;

/// <summary>
/// レビュー依頼サービスのインターフェース
/// </summary>
internal interface IReviewRequestService
{
    /// <summary>
    /// レビュー依頼を表示すべきかどうかを取得します
    /// </summary>
    bool ShouldShowReviewRequest { get; }

    /// <summary>
    /// レビュー依頼を表示します
    /// </summary>
    Task ShowReviewRequestAsync();

    /// <summary>
    /// Microsoft Storeのレビューページを開きます
    /// </summary>
    void OpenReviewPage();

    /// <summary>
    /// レビュー依頼を後で表示するように設定します
    /// </summary>
    Task ShowLaterAsync();

    /// <summary>
    /// レビュー依頼を二度と表示しないように設定します
    /// </summary>
    Task NeverShowAgainAsync();
}

/// <summary>
/// レビュー依頼サービスの実装
/// </summary>
internal class ReviewRequestService : BackgroundService, IReviewRequestService
{
    private const int DaysBeforeReview = 4;
    private const string ReviewStateFileName = "review-state.json";
    private static readonly string reviewStatePath = Path.Combine(PathUtility.UserDir, ReviewStateFileName);
    
    // Microsoft Store用のプロトコルURL
    private const string StoreReviewUrl = "ms-windows-store://review/?ProductId=9P4TWX8P72L9";
    
    private readonly ILogger<ReviewRequestService> logger;
    private readonly App app;
    private ReviewState? reviewState;

    public bool ShouldShowReviewRequest { get; private set; }

    public ReviewRequestService(ILogger<ReviewRequestService> logger, App app)
    {
        this.logger = logger;
        this.app = app;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsMicrosoftStoreVersion())
        {
            this.logger.LogInformation("Microsoft Store版ではないため、レビュー依頼は表示しません");
            return;
        }

        await this.app.WaitForStartupAsync();
        
        // Toast通知のアクティベーション処理を登録
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;

        this.reviewState = await LoadReviewStateAsync();

        if (this.reviewState.NeverShowAgain)
        {
            this.logger.LogInformation("レビュー依頼を二度と表示しない設定になっています");
            return;
        }

        if (this.reviewState.FirstLaunchDate == null)
        {
            // 初回起動日を記録
            this.reviewState = this.reviewState with { FirstLaunchDate = DateTime.UtcNow };
            await SaveReviewStateAsync(this.reviewState);
            this.logger.LogInformation("初回起動日を記録しました");
            return;
        }

        var daysSinceFirstLaunch = (DateTime.UtcNow - this.reviewState.FirstLaunchDate.Value).TotalDays;
        if (daysSinceFirstLaunch >= DaysBeforeReview)
        {
            this.ShouldShowReviewRequest = true;
            this.logger.LogInformation($"初回起動から {daysSinceFirstLaunch:F1} 日経過しました。レビュー依頼を表示します");
            
            // レビュー依頼通知を表示
            ShowReviewNotification();
        }
    }

    public async Task ShowReviewRequestAsync()
    {
        this.ShouldShowReviewRequest = false;
        
        // レビュー依頼を表示した日時を記録
        if (this.reviewState != null)
        {
            this.reviewState = this.reviewState with { LastShownDate = DateTime.UtcNow };
            await SaveReviewStateAsync(this.reviewState);
        }
    }

    public void OpenReviewPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(StoreReviewUrl) { UseShellExecute = true });
            this.logger.LogInformation("Microsoft Storeのレビューページを開きました");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Microsoft Storeのレビューページを開くことができませんでした");
        }
    }

    public async Task ShowLaterAsync()
    {
        this.ShouldShowReviewRequest = false;
        
        if (this.reviewState != null)
        {
            this.reviewState = this.reviewState with { LastShownDate = DateTime.UtcNow };
            await SaveReviewStateAsync(this.reviewState);
        }
        
        this.logger.LogInformation("レビュー依頼を後で表示するように設定しました");
    }

    public async Task NeverShowAgainAsync()
    {
        this.ShouldShowReviewRequest = false;
        
        if (this.reviewState != null)
        {
            this.reviewState = this.reviewState with { NeverShowAgain = true };
            await SaveReviewStateAsync(this.reviewState);
        }
        
        this.logger.LogInformation("レビュー依頼を二度と表示しない設定にしました");
    }

    /// <summary>
    /// レビュー依頼通知を表示します
    /// </summary>
    private void ShowReviewNotification()
    {
        var builder = new ToastContentBuilder()
            .AddText("WindowTranslatorをご利用いただきありがとうございます")
            .AddText("Microsoft Storeでレビューをお願いできますでしょうか？")
            .AddArgument(nameof(ReviewRequestService))
            .AddButton(new ToastButton()
                .AddArgument("action", ToastActions.Review)
                .SetContent("レビューする"))
            .AddButton(new ToastButton()
                .AddArgument("action", ToastActions.Later)
                .SetContent("後で")
                .SetBackgroundActivation());

        {
            var args = ToastArguments.Parse(builder.Content.Launch);
            args.Add("action", ToastActions.NeverShowAgain);
            builder.Content.Actions.ContextMenuItems.Add(new("二度と表示しない", args.ToString()));
        }

        builder.Show(t =>
        {
            t.ExpiresOnReboot = true;
            t.NotificationMirroring = NotificationMirroring.Disabled;
            t.SuppressPopup = false;
        });

        // レビュー依頼を表示したことを記録
        _ = ShowReviewRequestAsync();
    }

    /// <summary>
    /// Toast通知のアクティベーション処理
    /// </summary>
    private async void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        var args = ToastArguments.Parse(e.Argument);
        if (!args.Contains(nameof(ReviewRequestService)))
        {
            return;
        }

        if (!args.TryGetValue<ToastActions>("action", out var action))
        {
            return;
        }

        switch (action)
        {
            case ToastActions.Review:
                OpenReviewPage();
                await NeverShowAgainAsync();
                break;
            case ToastActions.Later:
                await ShowLaterAsync();
                break;
            case ToastActions.NeverShowAgain:
                await NeverShowAgainAsync();
                break;
        }
    }

    /// <summary>
    /// Toast通知のアクション
    /// </summary>
    private enum ToastActions
    {
        Review,
        Later,
        NeverShowAgain
    }

    /// <summary>
    /// Microsoft Store版かどうかを判定します
    /// </summary>
    private static bool IsMicrosoftStoreVersion()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath))
        {
            return false;
        }

        // WindowsAppsフォルダ内にインストールされているかチェック
        return processPath.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase);
    }

    private async ValueTask<ReviewState> LoadReviewStateAsync()
    {
        try
        {
            if (File.Exists(reviewStatePath))
            {
                using var fs = File.OpenRead(reviewStatePath);
                var state = await JsonSerializer.DeserializeAsync<ReviewState>(fs);
                return state ?? new ReviewState();
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "レビュー状態の読み込みに失敗しました");
        }
        return new ReviewState();
    }

    private async ValueTask SaveReviewStateAsync(ReviewState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reviewStatePath)!);
            using var fs = File.Create(reviewStatePath);
            await JsonSerializer.SerializeAsync(fs, state);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "レビュー状態の保存に失敗しました");
        }
    }
}

/// <summary>
/// レビューのダミーサービス（Microsoft Store版以外用）
/// </summary>
internal class IgnoreReviewRequestService : IReviewRequestService
{
    public bool ShouldShowReviewRequest => false;

    public Task ShowReviewRequestAsync() => Task.CompletedTask;

    public void OpenReviewPage() { }

    public Task ShowLaterAsync() => Task.CompletedTask;

    public Task NeverShowAgainAsync() => Task.CompletedTask;
}

/// <summary>
/// レビュー状態を保持するレコード
/// </summary>
internal record ReviewState
{
    /// <summary>
    /// 初回起動日
    /// </summary>
    public DateTime? FirstLaunchDate { get; init; }

    /// <summary>
    /// 最後にレビュー依頼を表示した日時
    /// </summary>
    public DateTime? LastShownDate { get; init; }

    /// <summary>
    /// 二度と表示しない設定
    /// </summary>
    public bool NeverShowAgain { get; init; }
}
