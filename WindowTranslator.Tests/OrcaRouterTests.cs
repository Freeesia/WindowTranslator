extern alias OrcaRouter;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using WindowTranslator.ComponentModel;
using OrcaRouterAuthentication = OrcaRouter::WindowTranslator.Plugin.OrcaRouterPlugin.OrcaRouterAuthentication;
using OrcaRouterModels = OrcaRouter::WindowTranslator.Plugin.OrcaRouterPlugin.OrcaRouterModels;
using OrcaRouterOptions = OrcaRouter::WindowTranslator.Plugin.OrcaRouterPlugin.OrcaRouterOptions;
using OrcaRouterTranslator = OrcaRouter::WindowTranslator.Plugin.OrcaRouterPlugin.OrcaRouterTranslator;

namespace WindowTranslator.Tests;

public class OrcaRouterTests
{
    [Fact]
    public void OptionsHideApiKeyAndUseDynamicModelItems()
    {
        var properties = TypeDescriptor.GetProperties(typeof(OrcaRouterOptions));
        var apiKey = Assert.IsAssignableFrom<PropertyDescriptor>(properties[nameof(OrcaRouterOptions.ApiKey)]);
        var model = Assert.IsAssignableFrom<PropertyDescriptor>(properties[nameof(OrcaRouterOptions.Model)]);

        Assert.False(apiKey.IsBrowsable);
        Assert.Null(apiKey.Attributes[typeof(DataTypeAttribute)]);
        Assert.NotNull(model.Attributes[typeof(DynamicItemsSourceAttribute)]);
    }

    [Fact]
    public void DiscoveryEndpointsAreRestrictedToTheOrcaRouterHost()
    {
        Assert.Equal("https://www.orcarouter.ai/auth", OrcaRouterAuthentication.NormalizeEndpoint("http://www.orcarouter.ai/auth").AbsoluteUri);
        Assert.Throws<InvalidOperationException>(() => OrcaRouterAuthentication.NormalizeEndpoint("https://attacker.example/auth"));
        Assert.Throws<InvalidOperationException>(() => OrcaRouterAuthentication.NormalizeEndpoint("https://www.orcarouter.ai:8443/auth"));
    }

    [Fact]
    public void PkceChallengeUsesSha256Base64Url()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", OrcaRouterAuthentication.CreateChallenge(verifier));
    }

    [Fact]
    public void ModelsUseApiNameAndPerMillionPrices()
    {
        using var document = JsonDocument.Parse("""
            {"data":[
              {"id":"google/gemini-test","name":"Gemini Test","supported_endpoint_types":["openai"],"architecture":{"output_modalities":["text"]},"pricing":{"prompt":"0.00000075","completion_per_million":"3.75"}},
              {"id":"image/test","name":"Image Test","supported_endpoint_types":["openai"],"architecture":{"output_modalities":["image"]},"pricing":{"prompt":"1","completion":"2"}}
            ]}
            """);

        var item = Assert.Single(OrcaRouterModels.ParseModels(document.RootElement));
        Assert.Equal("google/gemini-test", item.Value);
        Assert.Equal("Gemini Test ($0.75 / $3.75 per 1M)", item.DisplayName);
    }

    [Fact]
    public void TranslationResponseRequiresOneResultPerInput()
    {
        Assert.Equal(["翻訳1", "翻訳2"], OrcaRouterTranslator.ParseTranslation("""
            ```json
            {"translated":["翻訳1","翻訳2"]}
            ```
            """, 2));
        Assert.Throws<JsonException>(() => OrcaRouterTranslator.ParseTranslation("{\"translated\":[\"1件だけ\"]}", 2));
    }
}
