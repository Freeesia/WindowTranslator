using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace WindowTranslator;

/// <summary>
/// カスタムリソースマネージャー
/// </summary>
public class CustomResourceManager : ResourceManager
{
    /// <summary>
    /// コンストラクタ
    /// </summary>
    public CustomResourceManager(string baseName, System.Reflection.Assembly assembly)
        : base(baseName, assembly)
        => InitializeCultureFallback();

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public CustomResourceManager(Type resourceSource)
        : base(resourceSource)
        => InitializeCultureFallback();

    private void InitializeCultureFallback()
    {
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "ja")
        {
            GetNeutralResourcesCulture(this) = CultureInfo.GetCultureInfo("en");
            GetFallbackLoc(this) = UltimateResourceFallbackLocation.Satellite;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_neutralResourcesCulture")]
    static extern ref CultureInfo GetNeutralResourcesCulture(ResourceManager c);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fallbackLoc")]
    static extern ref UltimateResourceFallbackLocation GetFallbackLoc(ResourceManager c);
}
