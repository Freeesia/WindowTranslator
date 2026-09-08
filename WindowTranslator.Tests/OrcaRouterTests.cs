extern alias OrcaRouter;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using PropertyTools.DataAnnotations;
using OrcaRouterAuthentication = OrcaRouter::WindowTranslator.Plugin.OrcaRouterPlugin.OrcaRouterAuthentication;
using OrcaRouterModels = OrcaRouter::WindowTranslator.Plugin.OrcaRouterPlugin.OrcaRouterModels;
using OrcaRouterOptions = OrcaRouter::WindowTranslator.Plugin.OrcaRouterPlugin.OrcaRouterOptions;
using OrcaRouterTranslator = OrcaRouter::WindowTranslator.Plugin.OrcaRouterPlugin.OrcaRouterTranslator;

namespace WindowTranslator.Tests;

public class OrcaRouterTests
{
    [Fact]
    public void OptionsHideApiKeyAndUseStandardModelItemsSource()
    {
        var properties = TypeDescriptor.GetProperties(typeof(OrcaRouterOptions));
        var apiKey = Assert.IsAssignableFrom<PropertyDescriptor>(properties[nameof(OrcaRouterOptions.ApiKey)]);
        var model = Assert.IsAssignableFrom<PropertyDescriptor>(properties[nameof(OrcaRouterOptions.Model)]);
        var modelItems = Assert.IsAssignableFrom<PropertyDescriptor>(properties[nameof(OrcaRouterOptions.ModelItems)]);

        Assert.False(apiKey.IsBrowsable);
        Assert.False(modelItems.IsBrowsable);
        Assert.Null(apiKey.Attributes[typeof(DataTypeAttribute)]);
        var itemsSource = Assert.IsType<ItemsSourcePropertyAttribute>(model.Attributes[typeof(ItemsSourcePropertyAttribute)]);
        Assert.Equal(nameof(OrcaRouterOptions.ModelItems), itemsSource.PropertyName);
        Assert.IsType<DisplayMemberPathAttribute>(model.Attributes[typeof(DisplayMemberPathAttribute)]);
        Assert.IsType<SelectedValuePathAttribute>(model.Attributes[typeof(SelectedValuePathAttribute)]);
    }

    [Fact]
    public async Task DiscoveryAcceptsTheCurrentOrcaRouterResponse()
    {
        const string response = """
            {"authorization_endpoint":"http://www.orcarouter.ai/auth","code_challenge_methods_supported":["S256","plain"],"grant_types_supported":["authorization_code"],"issuer":"http://www.orcarouter.ai","response_types_supported":["code"],"token_endpoint":"http://www.orcarouter.ai/api/v1/auth/keys","token_endpoint_auth_methods_supported":["none"]}
            """;
        using var client = new HttpClient(new StaticResponseHandler(response));

        var endpoints = await OrcaRouterAuthentication.DiscoverAsync(client, CancellationToken.None);

        Assert.Equal("https://www.orcarouter.ai/auth", endpoints.Authorization.AbsoluteUri);
        Assert.Equal("https://www.orcarouter.ai/api/v1/auth/keys", endpoints.Token.AbsoluteUri);
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

    private sealed class StaticResponseHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
    }
}
