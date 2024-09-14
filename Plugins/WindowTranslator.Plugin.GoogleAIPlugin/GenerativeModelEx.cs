using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using GenerativeAI.Exceptions;
using GenerativeAI.Helpers;
using GenerativeAI.Models;
using GenerativeAI.Requests;
using GenerativeAI.Types;
using System.Net.Http.Json;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

class GenerativeModelEx(string apiKey, ModelParams modelParams, string? system = null) : GenerativeModel(apiKey, modelParams, systemInstruction: system)
{
    protected override async Task<EnhancedGenerateContentResponse> GenerateContent(string apiKey, string model, GenerateContentRequest request)
    {
        var url = new RequestUrl(model, Tasks.GenerateContent, apiKey, false, BaseUrl, Version);
        request.SystemInstruction ??= RequestExtensions.FormatSystemInstruction(this.SystemInstruction);
        var serializerOptions = SerializerOptions;
        serializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        serializerOptions.TypeInfoResolver = PolymorphicTypeResolver.Instance;

        var response = await Client.PostAsJsonAsync(url, request, serializerOptions).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<EnhancedGenerateContentResponse>(serializerOptions).ConfigureAwait(false);

            if (result!.Candidates is not { Length: > 0 })
            {
                var blockErrorMessage = ResponseHelper.FormatBlockErrorMessage(result);
                if (!string.IsNullOrEmpty(blockErrorMessage))
                {
                    throw new GenerativeAIException($"Error while requesting {url.ToString("__API_Key__")}:\r\n\r\n{blockErrorMessage}", blockErrorMessage);
                }
            }

            return result;
        }
        else
        {
            var res = await response.Content.ReadFromJsonAsync<GoogleAIResponse>();
            throw new GenerativeAIExException(res!.Error);
        }
    }

    private class EnhancedGenerateContentResponseEx : EnhancedGenerateContentResponse
    {
        public override string? Text()
            => base.Text() ?? throw new InvalidOperationException();
    }

    private class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
    {
        public static readonly PolymorphicTypeResolver Instance = new();
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var jsonTypeInfo = base.GetTypeInfo(type, options);
            if (jsonTypeInfo.Type == typeof(GenerationConfig))
            {
                jsonTypeInfo.PolymorphismOptions = new()
                {
                    IgnoreUnrecognizedTypeDiscriminators = true,
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor,
                    DerivedTypes = { new(typeof(GenerationConfigEx)) },
                };
            }

            return jsonTypeInfo;
        }
    }
}

class GenerationConfigEx : GenerationConfig
{
    public string ResponseMimeType { get; set; } = "text/plain";
}

record GoogleAIResponse(GoogleAIError Error);
record GoogleAIError(int Code, string Message, string Status);
class GenerativeAIExException(GoogleAIError error) : Exception(error.Message)
{
    public GoogleAIError Error { get; } = error;
}
