using System.IO;
using System.Text.Json;

namespace WindowTranslator.Stores;

/// <summary>
/// モデル名の利用履歴を管理するストアのインターフェースです。
/// </summary>
public interface IModelHistoryStore
{
    /// <summary>
    /// 指定キーの履歴を取得します。
    /// </summary>
    IReadOnlyList<string> GetHistory(string key);

    /// <summary>
    /// 指定キーに値を追加します。既存の値がある場合は先頭に移動します。
    /// </summary>
    void AddHistory(string key, string value);

    /// <summary>
    /// 履歴をファイルに保存します。
    /// </summary>
    void Save();
}

/// <inheritdoc cref="IModelHistoryStore"/>
public class ModelHistoryStore : IModelHistoryStore
{
    private static readonly string historyPath = Path.Combine(PathUtility.UserDir, "model-history.json");
    private const int MaxHistoryCount = 10;

    private readonly Dictionary<string, List<string>> history;

    /// <summary>
    /// <see cref="ModelHistoryStore"/> の新しいインスタンスを初期化します。
    /// </summary>
    public ModelHistoryStore()
    {
        if (File.Exists(historyPath))
        {
            try
            {
                using var fs = File.OpenRead(historyPath);
                this.history = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(fs) ?? [];
            }
            catch (JsonException)
            {
                this.history = [];
            }
            catch (IOException)
            {
                this.history = [];
            }
        }
        else
        {
            this.history = [];
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetHistory(string key)
        => this.history.TryGetValue(key, out var list) ? list.AsReadOnly() : [];

    /// <inheritdoc/>
    public void AddHistory(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!this.history.TryGetValue(key, out var list))
        {
            list = [];
            this.history[key] = list;
        }

        list.Remove(value);
        list.Insert(0, value);

        if (list.Count > MaxHistoryCount)
        {
            list.RemoveRange(MaxHistoryCount, list.Count - MaxHistoryCount);
        }
    }

    /// <inheritdoc/>
    public void Save()
    {
        Directory.CreateDirectory(PathUtility.UserDir);
        using var fs = File.Create(historyPath);
        JsonSerializer.Serialize(fs, this.history);
    }
}
