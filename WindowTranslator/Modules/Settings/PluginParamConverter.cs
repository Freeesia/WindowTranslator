using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowTranslator.Modules.Settings;
internal class PluginParamConverter : JsonConverter<IPluginParam>
{
    public override IPluginParam? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotImplementedException();

    public override void Write(Utf8JsonWriter writer, IPluginParam value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
