using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using WindowTranslator.Extensions;
using WindowTranslator.Properties;
using WindowTranslator.Stores;
using Wpf.Ui;

namespace WindowTranslator.Modules.LogView;

[OpenWindow]
public sealed partial class LogViewModel : ObservableObject, IDisposable
{
    private static readonly CompositeFormat ExportLogsSuccessDetail = CompositeFormat.Parse(Resources.ExportLogsSuccessDetail);
    private readonly ILogStore store;
    private readonly IPresentationService presentationService;
    private readonly ISnackbarService snackbarService;
    private readonly ObservableCollection<LogEntry> logs = [];
    private readonly Channel<LogEntry> channel = Channel.CreateUnbounded<LogEntry>(new() { SingleReader = true });
    private readonly Task task;

    public ObservableCollection<LogEntry> Logs => logs;

    public LogViewModel([Inject] ILogStore logStore, [Inject] IPresentationService presentationService, [Inject] ISnackbarService snackbarService)
    {
        this.store = logStore;
        this.presentationService = presentationService;
        this.snackbarService = snackbarService;

        // 既存のログを読み込み
        foreach (var log in this.store.GetLogs())
        {
            logs.Add(log);
        }

        // 新しいログが追加された時のイベントハンドラーを登録
        this.task = Task.Run(ReadLogsAsync);
        this.store.LogAdded += OnLogAdded;

        // UIスレッドでコレクションビューを作成してバインド
        BindingOperations.EnableCollectionSynchronization(logs, new object());
    }

    private async Task ReadLogsAsync()
    {
        var list = new List<LogEntry>();
        while (await this.channel.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (this.channel.Reader.TryRead(out var logEntry))
            {
                list.Add(logEntry);
            }
            // UIスレッドで実行
            App.Current.Dispatcher.Invoke(() =>
            {
                foreach (var log in list)
                {
                    logs.Add(log);
                }
                list.Clear();

                // ログが多すぎる場合は古いものを削除
                while (logs.Count > 10000)
                {
                    logs.RemoveAt(0);
                }
            });
        }
    }

    public void Dispose()
    {
        this.store.LogAdded -= OnLogAdded;
        this.channel.Writer.Complete();
    }

    private void OnLogAdded(object? sender, LogEntry logEntry)
        => this.channel.Writer.TryWrite(logEntry);

    [RelayCommand]
    private void ClearLogs()
    {
        store.Clear();
        logs.Clear();
    }

    [RelayCommand]
    private void ExportLogs()
    {
        var context = new SaveFileDialogContext()
        {
            Title = Resources.ExportLogs,
            DefaultExtension = "txt",
            Filters = [new(Resources.ExportLogsFilterText, "txt")],
            DefaultFileName = $"WindowTranslator_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        var result = this.presentationService.SaveFile(context);

        if (result != DialogResult.Ok)
        {
            return;
        }

        try
        {
            var sb = new StringBuilder();
            foreach (var log in logs)
            {
                sb.AppendLine(CultureInfo.CurrentCulture, $"{log.Timestamp:s} | {log.Level, -12} | {log.Category} | {log.FormattedMessage}");
            }

            File.WriteAllText(context.FileName, sb.ToString(), Encoding.UTF8);
            this.snackbarService.ShowSuccess(Resources.ExportLogsSuccess, string.Format(CultureInfo.CurrentCulture, ExportLogsSuccessDetail, context.FileName));
        }
        catch (Exception ex)
        {
            this.snackbarService.ShowError(Resources.ExportLogsFailed, ex.Message);
        }
    }
}
