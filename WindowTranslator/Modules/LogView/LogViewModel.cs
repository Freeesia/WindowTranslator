using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kamishibai;
using Microsoft.Win32;
using WindowTranslator.Stores;

namespace WindowTranslator.Modules.LogView;

[OpenWindow]
public sealed partial class LogViewModel : ObservableObject, IDisposable
{
    private readonly ILogStore store;
    private readonly ObservableCollection<LogEntry> logs = [];
    private readonly Channel<LogEntry> channel = Channel.CreateUnbounded<LogEntry>(new() { SingleReader = true });
    private readonly Task task;

    public ObservableCollection<LogEntry> Logs => logs;

    public LogViewModel([Inject] ILogStore loggerService)
    {
        this.store = loggerService;

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
        // TODO: 実装整理
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
            FileName = $"WindowTranslator_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("WindowTranslator ログエクスポート");
                sb.AppendLine($"エクスポート日時: {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
                sb.AppendLine(new string('=', 50));
                sb.AppendLine();

                foreach (var log in logs)
                {
                    sb.AppendLine($"[{log.Timestamp:yyyy/MM/dd HH:mm:ss}] [{log.Level}] {log.Category}  {log.FormattedMessage}");
                }

                File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

            }
            catch (Exception ex)
            {
            }
        }
    }
}
