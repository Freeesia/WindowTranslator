using System.Text.RegularExpressions;

namespace WindowTranslator;

/// <summary>
/// OCRに関連するユーティリティメソッドを提供します。
/// </summary>
public static partial class OcrUtility
{

    [GeneratedRegex(@"^[\s\p{S}\p{P}\d]+$")]
    public static partial Regex AllSymbolOrSpace();
}