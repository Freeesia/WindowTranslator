using System.Reflection;
using System.Resources;

namespace WindowTranslator;

/// <summary>
/// リソースに関連するユーティリティメソッドを提供します。
/// </summary>
public static class ResourceUtility
{
    private static readonly Dictionary<Assembly, CustomResourceManager> ResManagerCache = new();

    /// <summary>
    /// 型からリソースマネージャを取得します。
    /// </summary>
    /// <param name="declaringType">型</param>
    /// <returns>リソースマネージャー</returns>
    public static ResourceManager? GetResourceManager(this Type declaringType)
    {
        var assembly = declaringType.Assembly;
        if (ResManagerCache.TryGetValue(assembly, out var resManager))
        {
            return resManager;
        }
        var ns = declaringType.Namespace;
        Type? resType = null;
        while (resType is null && ns is not null)
        {
            resType = assembly.GetType($"{ns}.Properties.Resources");
            int lastDotIndex = ns.LastIndexOf('.');
            ns = lastDotIndex > 0 ? ns[..lastDotIndex] : null;
        }
        if (resType is null)
        {
            return null;
        }
        return ResManagerCache[assembly] = new(resType);
    }
}
