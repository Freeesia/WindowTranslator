﻿using System.Drawing;

namespace WindowTranslator;

/// <summary>
/// 翻訳テキストの矩形情報
/// </summary>
/// <param name="Text">
/// テキスト
/// <paramref name="IsTranslated"/> が true の場合は翻訳後のテキスト
/// </param>
/// <param name="X">X位置（左上角のX座標）</param>
/// <param name="Y">Y位置（左上角のY座標）</param>
/// <param name="Width">幅</param>
/// <param name="Height">高さ</param>
/// <param name="FontSize">フォントサイズ</param>
/// <param name="MultiLine">複数行かどうか</param>
/// <param name="Foreground">文字色</param>
/// <param name="Background">背景色</param>
/// <param name="IsTranslated"><paramref name="Text"/>が翻訳後のテキストかどうか</param>
public record TextRect(string Text, double X, double Y, double Width, double Height, double FontSize, bool MultiLine, Color Foreground, Color Background, bool IsTranslated = false)
    : TextInfo(Text, IsTranslated)
{
    /// <summary>
    /// 空
    /// </summary>
    public static TextRect Empty { get; } = new TextRect(string.Empty, 0, 0, 0, 0, 0, false);

    /// <summary>
    /// 左上を中心とした回転角度（度数法、時計回り）
    /// </summary>
    public double Angle { get; init; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="text">テキスト</param>
    /// <param name="x">X位置</param>
    /// <param name="y">Y位置</param>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    /// <param name="fontSize">フォントサイズ</param>
    /// <param name="multiLine">複数行かどうか</param>
    public TextRect(string text, double x, double y, double width, double height, double fontSize, bool multiLine)
        : this(text, x, y, width, height, fontSize, multiLine, Color.Red, Color.WhiteSmoke)
    {
    }
};

/// <summary>
/// 翻訳テキストの矩形情報
/// </summary>
/// <param name="Text">
/// テキスト
/// <paramref name="IsTranslated"/> が true の場合は翻訳後のテキスト
/// </param>
/// <param name="IsTranslated"><paramref name="Text"/>が翻訳後のテキストかどうか</param>
public record TextInfo(string Text, bool IsTranslated = false)
{
    /// <summary>
    /// このテキストの文脈
    /// </summary>
    public string Context { get; init; } = string.Empty;
};