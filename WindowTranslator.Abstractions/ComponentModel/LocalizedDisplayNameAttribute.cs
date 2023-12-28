using System.Collections.Concurrent;
using System.ComponentModel;
using System.Resources;

namespace WindowTranslator.ComponentModel;
public class LocalizedDisplayNameAttribute(Type resourceType, string description) : DisplayNameAttribute(description)
{
    private static readonly ConcurrentDictionary<Type, ResourceManager> resourceCache = new();
    public Type ResourceType { get; set; } = resourceType;

    public override string DisplayName
    {
        get
        {
            var resourceManager = resourceCache.GetOrAdd(ResourceType, type => new ResourceManager(type));
            return resourceManager.GetString(this.DisplayNameValue) ?? this.DisplayNameValue;
        }
    }
}
