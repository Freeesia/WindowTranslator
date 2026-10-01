namespace WindowTranslator.Stores;

/// <summary>
/// 翻訳対象のプロセス情報を保持するインターフェース
/// </summary>
public interface IProcessInfoStore
{
    /// <summary>
    /// 対象のウィンドウまたはモニターのハンドル
    /// </summary>
    IntPtr TargetHandle { get; }

    /// <summary>
    /// 翻訳対象の設定名
    /// </summary>
    string Name { get; }
}
