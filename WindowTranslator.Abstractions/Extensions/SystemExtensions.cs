namespace WindowTranslator.Extensions;

/// <summary>
/// 汎用的な拡張メソッド
/// </summary>
public static class SystemExtensions
{
    /// <summary>
    /// 指定した値が指定した範囲内にあるかどうかを判定します。
    /// </summary>
    /// <typeparam name="T">型</typeparam>
    /// <param name="target">判定する対象</param>
    /// <param name="values">判定する範囲</param>
    /// <returns>含まれているかどうか</returns>
    public static bool Or<T>(this T target, params T[] values)
        where T : Enum
        => values.Contains(target);
}
