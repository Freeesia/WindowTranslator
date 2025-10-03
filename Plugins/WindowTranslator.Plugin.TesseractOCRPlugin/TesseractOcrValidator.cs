using System.Diagnostics;
using Microsoft.Win32;
using Octokit;
using TesseractOCR.Enums;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.TesseractOCRPlugin.Properties;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace WindowTranslator.Plugin.TesseractOCRPlugin;

public class TesseractOcrValidator(IGitHubClient client, IContentDialogService dialogService) : ITargetSettingsValidator
{
    private static readonly ValidateResult Invalid = ValidateResult.Invalid("TesseractOcr", Resources.NotInstalledVcRedist);
    private readonly IGitHubClient client = client;
    private readonly IContentDialogService dialogService = dialogService;

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(IOcrModule)] != nameof(TesseractOcr))
        {
            return ValidateResult.Valid;
        }

        // VC++ Redistributableの確認
        if (!IsVCRedistInstalled())
        {
            var r = await this.dialogService.ShowSimpleDialogAsync(new()
            {
                Title = "TesseractOcr",
                Content = Resources.ConfirmInstall,
                PrimaryButtonText = Resources.Install,
                CloseButtonText = Resources.Cancel
            });

            if (r != ContentDialogResult.Primary)
            {
                return Invalid;
            }

            var installCts = new CancellationTokenSource();
            var dialogCts = new CancellationTokenSource();
            var task = Task.Run(() => DownloadAndInstallVCRedistAsync(installCts.Token), installCts.Token)
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        dialogCts.Cancel();
                    }
                }, TaskScheduler.Default);

            try
            {
                await this.dialogService.ShowSimpleDialogAsync(new()
                {
                    Title = Resources.InstallingTitle,
                    Content = Resources.InstallingMessage,
                    CloseButtonText = Resources.Cancel
                }, dialogCts.Token);
                await installCts.CancelAsync();
                return Invalid;
            }
            catch (OperationCanceledException)
            {
                // ダイアログが閉じられた（インストール完了）
            }

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合
            }

            // 最終確認
            if (!IsVCRedistInstalled())
            {
                return Invalid;
            }
        }

        Directory.CreateDirectory(TesseractOcr.DataDir);
        var langData = LanguageHelper.EnumToString(TesseractOcr.ConvertLanguage(settings.Language.Source)) + ".traineddata";
        var langDataPath = Path.Combine(TesseractOcr.DataDir, langData);
        if (File.Exists(langDataPath))
        {
            return ValidateResult.Valid;
        }

        // IGitHubClient を利用して `tesseract-ocr/tessdata_best` リポジトリからeng.traineddataをダウンロードする
        var contents = await client.Repository.Content.GetRawContent("tesseract-ocr", "tessdata_best", langData);
        await using var fs = File.Create(langDataPath);
        await fs.WriteAsync(contents).ConfigureAwait(false);
        return ValidateResult.Valid;
    }

    private static async Task DownloadAndInstallVCRedistAsync(CancellationToken token)
    {
        // 一時フォルダにダウンロードして実行する
        const string vcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
        var tempDir = Path.Combine(Path.GetTempPath(), "WindowTranslator");
        Directory.CreateDirectory(tempDir);
        var installerPath = Path.Combine(tempDir, "vc_redist.x64.exe");
        try
        {
            // インストーラーをダウンロード
            if (!File.Exists(installerPath))
            {
                using var httpClient = new HttpClient();
                var data = await httpClient.GetByteArrayAsync(vcRedistUrl, token);
                await File.WriteAllBytesAsync(installerPath, data, token);
            }

            // インストーラーを実行
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/install /quiet /norestart",
                UseShellExecute = true,
                Verb = "runas" // 管理者権限で実行
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(token);
            }

            // インストール完了を確認
            while (!IsVCRedistInstalled())
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセルされた場合は何もしない
        }
    }

    /// <summary>
    /// Visual C++ Redistributableがインストールされているか確認します
    /// </summary>
    /// <returns>インストールされている場合はtrue</returns>
    private static bool IsVCRedistInstalled()
    {
        // VS2015以降の統合版ランタイム (x64)
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
            return key?.GetValue("Installed") is int installed && installed == 1;
        }
        catch
        {
            // レジストリアクセスエラーは無視
        }
        return false;
    }
}
