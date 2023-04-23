using PropertyTools.Wpf;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Windows.Data;

namespace WindowTranslator.Modules.Settings;
internal class SettingsPropertyGridOperator : PropertyGridOperator
{
    protected override IEnumerable<PropertyItem> CreatePropertyItems(object instance, IPropertyGridOptions options)
    {
        var instanceType = instance.GetType();
        var metadataTypeAttribute = instanceType.GetCustomAttributes(typeof(MetadataTypeAttribute), inherit: true).OfType<MetadataTypeAttribute>().FirstOrDefault();
        PropertyDescriptorCollection properties;
        if (metadataTypeAttribute != null)
        {
            instanceType = metadataTypeAttribute.MetadataClassType;
            properties = TypeDescriptor.GetProperties(instanceType);
        }
        else
        {
            properties = TypeDescriptor.GetProperties(instance);
        }

        foreach (PropertyDescriptor pd in properties)
        {
            if (options.ShowDeclaredOnly && pd.ComponentType != instanceType)
            {
                continue;
            }
            var firstAttributeOrDefault = pd.GetFirstAttributeOrDefault<PropertyTools.DataAnnotations.BrowsableAttribute>();
            if (firstAttributeOrDefault != null && !firstAttributeOrDefault.Browsable || !pd.IsBrowsable || !options.ShowReadOnlyProperties && pd.IsReadOnly() || options.RequiredAttribute != null && pd.GetFirstAttributeOrDefault(options.RequiredAttribute) == null)
            {
                continue;
            }

            if (typeof(IReadOnlyList<IPluginParam>).IsAssignableFrom(pd.PropertyType))
            {
                var @params = (IReadOnlyList<IPluginParam>)pd.GetValue(instance)!;
                for (int i = 0; i < @params.Count; i++)
                {
                    var param = @params[i];
                    foreach (ParentablePropertyItem item in CreatePropertyItems(param, options).Cast<ParentablePropertyItem>())
                    {
                        item.AddParent($"{pd.Name}[{i}]");
                        yield return item;
                    }
                }
            }
            else
            {
                yield return CreatePropertyItem(pd, properties, instance);
            }
        }
    }

    protected override PropertyItem CreateCore(PropertyDescriptor pd, PropertyDescriptorCollection propertyDescriptors)
        => new ParentablePropertyItem(pd, propertyDescriptors);

    private class ParentablePropertyItem : PropertyItem
    {
        private readonly Stack<string> parents = new();
        public ParentablePropertyItem(PropertyDescriptor propertyDescriptor, PropertyDescriptorCollection propertyDescriptors)
            : base(propertyDescriptor, propertyDescriptors)
        {
        }

        public void AddParent(string parent)
            => parents.Push(parent);

        public override Binding CreateBinding(UpdateSourceTrigger trigger = UpdateSourceTrigger.Default, bool applyConverter = true)
        {
            var binding = base.CreateBinding(trigger, applyConverter);
            binding.Path.Path = string.Join('.', parents.Append(binding.Path.Path));
            return binding;
        }
    }
}
