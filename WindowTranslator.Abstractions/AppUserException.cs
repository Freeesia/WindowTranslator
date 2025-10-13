namespace WindowTranslator;

/// <summary>
/// ユーザの操作ミスなど、アプリケーションの使用方法に起因する例外を表します。
/// </summary>
/// <param name="message">エラーメッセージ</param>
/// <param name="innerException">内部例外</param>
public class AppUserException(string? message, Exception? innerException) : Exception(message, innerException)
{
    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    public AppUserException(string? message)
        : this(message, null)
    {
    }
}
