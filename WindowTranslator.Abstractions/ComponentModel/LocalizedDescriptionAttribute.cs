using System.Collections.Concurrent;
using System.ComponentModel;
using System.Resources;

namespace WindowTranslator.ComponentModel;
public class LocalizedDescriptionAttribute(Type resourceType, string description) : DescriptionAttribute(description)
{
    private static readonly ConcurrentDictionary<Type, ResourceManager> resourceCache = new();
    public Type ResourceType { get; set; } = resourceType;

    public override string Description
    {
        get
        {
            var resourceManager = resourceCache.GetOrAdd(ResourceType, type => new ResourceManager(type));
            return resourceManager.GetString(this.DescriptionValue) ?? base.Description;
        }
    }
}
