﻿namespace WindowTranslator;

/// <summary>
/// 設定の検証を行うインターフェース
/// </summary>
public interface ITargetSettingsValidator
{
    /// <summary>
    /// 設定の検証を行う
    /// </summary>
    /// <returns>検証結果</returns>
    ValueTask<ValidateResult> Validate(TargetSettings settings);
}

/// <summary>
/// 検証結果
/// </summary>
/// <param name="Title">タイトル</param>
/// <param name="IsValid">検証結果</param>
/// <param name="Message">検証エラーのメッセージ</param>
public record ValidateResult(bool IsValid, string Title, string Message)
{
    /// <summary>
    /// 検証結果が有効であることを示す
    /// </summary>
    public static ValidateResult Valid { get; } = new(true, string.Empty, string.Empty);

    /// <summary>
    /// 検証結果が無効であることを示す
    /// </summary>
    /// <param name="title">タイトル</param>
    /// <param name="message">検証エラーのメッセージ</param>
    /// <returns>検証結果</returns>
    public static ValidateResult Invalid(string title, string message) => new(false, title, message);
}