//#define ENABLE_REVIEW

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
    /// レビュー依頼を表示できるかどうか
    /// </summary>
    bool CanOpenReview { get; }

    /// <summary>
    /// Microsoft Storeのレビューページを開きます
    /// </summary>
    Task OpenReviewPageAsync();
}

/// <summary>
/// レビュー依頼サービスの実装
/// </summary>
internal class ReviewRequestService(ILogger<ReviewRequestService> logger, App app) : BackgroundService, IReviewRequestService
{
    private const int DaysBeforeReview = 4;
    private const string ReviewStateFileName = "review-state.json";
    private static readonly string reviewStatePath = Path.Combine(PathUtility.UserDir, ReviewStateFileName);

    // Microsoft Store用のプロトコルURL
    private const string StoreReviewUrl = "ms-windows-store://review/?ProductId=9pjd2fdzqxm3";

    private readonly ILogger<ReviewRequestService> logger = logger;
    private readonly App app = app;
    private ReviewState? reviewState;

    public bool CanOpenReview { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsMicrosoftStoreVersion())
        {
            return;
        }
        this.CanOpenReview = true;

        await this.app.WaitForStartupAsync();

        // Toast通知のアクティベーション処理を登録
        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;

        this.reviewState = await LoadReviewStateAsync();

        if (this.reviewState.NeverShowAgain)
        {
            return;
        }

        // 今日の日付を記録
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        if (!this.reviewState.LaunchDates.Contains(today))
        {
            this.reviewState = this.reviewState with { LaunchDates = [.. this.reviewState.LaunchDates, today] };
            await SaveReviewStateAsync(this.reviewState);
        }

        // 起動日数が指定日数以上になったらレビュー依頼を表示
        if (this.reviewState.LaunchDates.Count >= DaysBeforeReview)
        {
            // レビュー依頼通知を表示
            ShowReviewNotification();
        }
    }

    public async Task OpenReviewPageAsync()
    {
        try
        {
            Process.Start(new ProcessStartInfo(StoreReviewUrl) { UseShellExecute = true });
            this.logger.LogDebug("Microsoft Storeのレビューページを開きました");

            // Reviewedフラグを立てる
            this.reviewState = (this.reviewState ?? await LoadReviewStateAsync()) with { NeverShowAgain = true };
            await SaveReviewStateAsync(this.reviewState);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Microsoft Storeのレビューページを開くことができませんでした");
        }
    }

    private async Task ShowLaterAsync()
    {
        // 後で表示するように起動日付リストをクリア(再度カウントを始める)
        this.reviewState = (this.reviewState ?? await LoadReviewStateAsync()) with { LaunchDates = [] };
        await SaveReviewStateAsync(this.reviewState);
        this.logger.LogDebug("レビュー依頼を後で表示するように設定しました");
    }

    private async Task NeverShowAgainAsync()
    {
        // 二度と表示しない設定にする
        this.reviewState = (this.reviewState ?? await LoadReviewStateAsync()) with { NeverShowAgain = true };
        await SaveReviewStateAsync(this.reviewState);
        this.logger.LogDebug("レビュー依頼を二度と表示しない設定にしました");
    }

    /// <summary>
    /// レビュー依頼通知を表示します
    /// </summary>
    private void ShowReviewNotification()
    {
        var builder = new ToastContentBuilder()
            .AddText(Properties.Resources.ReviewRequest)
            .AddText(Properties.Resources.ReviewRequestMessage)
            .AddArgument(nameof(ReviewRequestService))
            .AddButton(new ToastButton()
                .AddArgument("action", ToastActions.Review)
                .SetContent(Properties.Resources.WriteReview))
            .AddButton(new ToastButton()
                .AddArgument("action", ToastActions.Later)
                .SetContent(Properties.Resources.ReviewLater)
                .SetBackgroundActivation());

        {
            var args = ToastArguments.Parse(builder.Content.Launch);
            args.Add("action", ToastActions.NeverShowAgain);
            builder.Content.Actions.ContextMenuItems.Add(new(Properties.Resources.ReviewNeverShowAgain, args.ToString()));
        }

        builder.Show(t =>
        {
            t.ExpiresOnReboot = true;
            t.NotificationMirroring = NotificationMirroring.Disabled;
            t.SuppressPopup = false;
        });
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
                await OpenReviewPageAsync();
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
#if ENABLE_REVIEW
        return true;
#endif
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
/// レビュー状態を保持するレコード
/// </summary>
internal record ReviewState
{
    /// <summary>
    /// 起動した日付のリスト（日を跨いだ起動のみカウント）
    /// </summary>
    public HashSet<string> LaunchDates { get; init; } = [];

    /// <summary>
    /// 二度と表示しない設定
    /// </summary>
    /// <remarks>
    /// レビューを書いた場合もこのフラグを立てる
    /// </remarks>
    public bool NeverShowAgain { get; init; }
}
