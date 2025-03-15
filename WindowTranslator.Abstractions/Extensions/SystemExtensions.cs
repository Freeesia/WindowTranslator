namespace WindowTranslator.Extensions;

public static class SystemExtensions
{
    public static bool Or<T>(this T target, params T[] values)
        where T : Enum
        => values.Contains(target);
}
