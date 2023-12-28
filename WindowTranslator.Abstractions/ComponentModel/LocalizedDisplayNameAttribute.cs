using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace WindowTranslator.ComponentModel;

/// <summary>
/// ローカライズされた表示名を提供します。
/// </summary>
/// <param name="resourceType">リソースの型</param>
/// <param name="displayName">キー</param>
public class LocalizedDisplayNameAttribute(Type resourceType, string displayName) : DisplayNameAttribute(displayName)
{
    private static readonly ConcurrentDictionary<Type, ResourceManager> resourceCache = new();

    /// <summary>
    /// リソースの型を取得または設定します。
    /// </summary>
    public Type ResourceType { get; set; } = resourceType;

    /// <inheritdoc/>
    public override string DisplayName
    {
        get
        {
            var resourceManager = resourceCache.GetOrAdd(ResourceType, type => new ResourceManager(type));
            return resourceManager.GetString(this.DisplayNameValue, CultureInfo.CurrentUICulture) ?? this.DisplayNameValue;
        }
    }
}
