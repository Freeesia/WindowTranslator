using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace WindowTranslator.ComponentModel;

/// <summary>
/// ローカライズされた説明を提供します。
/// </summary>
/// <param name="resourceType">リソースの型</param>
/// <param name="description">キー</param>
public class LocalizedDescriptionAttribute(Type resourceType, string description) : DescriptionAttribute(description)
{
    private static readonly ConcurrentDictionary<Type, ResourceManager> resourceCache = new();

    /// <summary>
    /// リソースの型を取得または設定します。
    /// </summary>
    public Type ResourceType { get; set; } = resourceType;

    /// <inheritdoc/>
    public override string Description
    {
        get
        {
            var resourceManager = resourceCache.GetOrAdd(ResourceType, type => new ResourceManager(type));
            return resourceManager.GetString(this.DescriptionValue, CultureInfo.CurrentUICulture) ?? this.DescriptionValue;
        }
    }
}
