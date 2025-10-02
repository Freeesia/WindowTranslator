namespace WindowTranslator;

/// <summary>
/// ユーザの操作ミスなど、アプリケーションの使用方法に起因する例外を表します。
/// </summary>
/// <param name="message"></param>
public class AppUserException(string? message) : Exception(message);
