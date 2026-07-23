namespace WindowTranslator.Stores;

/// <summary>
/// 翻訳対象のプロセス情報を保持するインターフェース
/// </summary>
public interface IProcessInfoStore
{
    /// <summary>
    /// 対象のウィンドウハンドル
    /// </summary>
    IntPtr MainWindowHandle { get; }

    /// <summary>
    /// 対象のプロセスの名前
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 対象がモニター（ディスプレイ）かどうか
    /// </summary>
    bool IsMonitor { get; }
}
