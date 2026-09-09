extern alias OrcaRouter;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
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
        var options = new OrcaRouterOptions();
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
        Assert.Equal(OrcaRouterModels.FreeModel, options.Model);
        Assert.Equal([OrcaRouterModels.FreeModel, OrcaRouterModels.AutoModel], options.ModelItems.Select(item => item.Value));
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
    public void ModelsAcceptCurrentFreeCatalogEntriesWithNullCapabilities()
    {
        using var document = JsonDocument.Parse("""
            {"data":[
              {"id":"deepseek/deepseek-v4-flash-free","name":"DeepSeek V4 Flash (Free)","supported_endpoint_types":null,"pricing":{"request":"0.000000"}},
              {"id":"codex-auto-review","supported_endpoint_types":null,"pricing":{"prompt_per_million":"0.2","completion_per_million":"1.2"}},
              {"id":"deepseek/deepseek-reasoner","supported_endpoint_types":["openai"],"architecture":{"output_modalities":null},"pricing":{"prompt_per_million":"0.147","completion_per_million":"0.295"}}
            ]}
            """);

        var items = OrcaRouterModels.ParseModels(document.RootElement).ToArray();

        Assert.Equal(2, items.Length);
        Assert.Equal("DeepSeek V4 Flash (Free) ($0 / $0 per 1M)", items[0].DisplayName);
        Assert.Equal("deepseek/deepseek-reasoner ($0.147 / $0.295 per 1M)", items[1].DisplayName);
    }

    [Fact]
    public async Task ModelCatalogRequestUsesOrcaRouterEndpointAndApiKey()
    {
        var handler = new RecordingResponseHandler("""
            {"data":[{"id":"tencent/hy3-free","name":"Tencent: Hy3 (Free)","supported_endpoint_types":null,"pricing":{"request":"0.000000"}}]}
            """);
        using var client = new HttpClient(handler);

        var items = await OrcaRouterModels.GetItemsAsync(client, "sk-orca-test", OrcaRouterModels.FreeModel, CancellationToken.None);

        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal($"{OrcaRouterModels.Endpoint}/models", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("sk-orca-test", handler.AuthorizationParameter);
        Assert.Equal([OrcaRouterModels.FreeModel, OrcaRouterModels.AutoModel, "tencent/hy3-free"], items.Select(item => item.Value));
    }

    [Fact]
    public async Task TranslationRequestUsesFreeModelAndOrcaRouterEndpoint()
    {
        var handler = new RecordingResponseHandler("""
            {"id":"test","object":"chat.completion","created":0,"model":"orcarouter/free","choices":[{"index":0,"message":{"role":"assistant","content":"{\"translated\":[\"Hello\"]}"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
            """);
        using var httpClient = new HttpClient(handler);
        var client = new ChatClient(OrcaRouterModels.FreeModel, new ApiKeyCredential("sk-orca-test"), new OpenAIClientOptions
        {
            Endpoint = new Uri(OrcaRouterModels.Endpoint),
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
        });
        var translator = new OrcaRouterTranslator(new OrcaRouterOptions(), new LanguageOptions
        {
            Source = "ja-JP",
            Target = "en-US",
        }, client);

        var translated = await translator.TranslateAsync([new TextInfo("こんにちは", null)]);

        Assert.Equal(["Hello"], translated);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal($"{OrcaRouterModels.Endpoint}/chat/completions", handler.RequestUri?.AbsoluteUri);
        using var request = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.Equal(OrcaRouterModels.FreeModel, request.RootElement.GetProperty("model").GetString());
        var responseFormat = request.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", responseFormat.GetProperty("type").GetString());
        Assert.True(responseFormat.GetProperty("json_schema").GetProperty("strict").GetBoolean());
    }

    [Fact]
    public async Task AnthropicModelDoesNotUseStructuredOutput()
    {
        var handler = new RecordingResponseHandler("""
            {"id":"test","object":"chat.completion","created":0,"model":"anthropic/claude-sonnet-test","choices":[{"index":0,"message":{"role":"assistant","content":"{\"translated\":[\"Hello\"]}"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
            """);
        using var httpClient = new HttpClient(handler);
        var client = new ChatClient("anthropic/claude-sonnet-test", new ApiKeyCredential("sk-orca-test"), new OpenAIClientOptions
        {
            Endpoint = new Uri(OrcaRouterModels.Endpoint),
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
        });
        var translator = new OrcaRouterTranslator(new OrcaRouterOptions { Model = "anthropic/claude-sonnet-test" }, new LanguageOptions
        {
            Source = "ja-JP",
            Target = "en-US",
        }, client);

        Assert.Equal(["Hello"], await translator.TranslateAsync([new TextInfo("こんにちは", null)]));

        using var request = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        Assert.False(request.RootElement.TryGetProperty("response_format", out _));
    }

    [Fact]
    public async Task UnsupportedStructuredOutputFallsBackToPromptOnlyJson()
    {
        var handler = new SequenceResponseHandler(
            (HttpStatusCode.BadRequest, """
                {"error":{"message":"response_format json_schema is not supported by this model","type":"orcarouter_api_error","code":"api_not_implemented"}}
                """),
            (HttpStatusCode.OK, """
                {"id":"test","object":"chat.completion","created":0,"model":"orcarouter/auto","choices":[{"index":0,"message":{"role":"assistant","content":"{\"translated\":[\"Hello\"]}"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
                """));
        using var httpClient = new HttpClient(handler);
        var client = new ChatClient(OrcaRouterModels.AutoModel, new ApiKeyCredential("sk-orca-test"), new OpenAIClientOptions
        {
            Endpoint = new Uri(OrcaRouterModels.Endpoint),
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
        });
        var translator = new OrcaRouterTranslator(new OrcaRouterOptions { Model = OrcaRouterModels.AutoModel }, new LanguageOptions
        {
            Source = "ja-JP",
            Target = "en-US",
        }, client);

        Assert.Equal(["Hello"], await translator.TranslateAsync([new TextInfo("こんにちは", null)]));

        Assert.Equal(2, handler.RequestBodies.Count);
        using var structuredRequest = JsonDocument.Parse(handler.RequestBodies[0]);
        using var fallbackRequest = JsonDocument.Parse(handler.RequestBodies[1]);
        Assert.True(structuredRequest.RootElement.TryGetProperty("response_format", out _));
        Assert.False(fallbackRequest.RootElement.TryGetProperty("response_format", out _));
    }

    [Theory]
    [InlineData(402)] // 実サービスで観測した応答
    [InlineData(403)] // OrcaRouter の公開仕様
    public async Task CreditShortageIsReportedAsUserError(int statusCode)
    {
        var handler = new RecordingResponseHandler("""
            {"error":{"message":"You've run out of credits -- this request needs $0.0003.","type":"orcarouter_api_error","code":"insufficient_user_quota"}}
            """, (HttpStatusCode)statusCode);
        using var httpClient = new HttpClient(handler);
        var client = new ChatClient(OrcaRouterModels.AutoModel, new ApiKeyCredential("sk-orca-test"), new OpenAIClientOptions
        {
            Endpoint = new Uri(OrcaRouterModels.Endpoint),
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
        });
        var translator = new OrcaRouterTranslator(new OrcaRouterOptions { Model = OrcaRouterModels.AutoModel }, new LanguageOptions
        {
            Source = "ja-JP",
            Target = "en-US",
        }, client);

        var error = await Assert.ThrowsAsync<AppUserException>(async ()
            => await translator.TranslateAsync([new TextInfo("こんにちは", null)]));

        Assert.Contains("OrcaRouter", error.Message);
        Assert.IsType<ClientResultException>(error.InnerException);
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

    private sealed class RecordingResponseHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Method = request.Method;
            this.RequestUri = request.RequestUri;
            this.AuthorizationScheme = request.Headers.Authorization?.Scheme;
            this.AuthorizationParameter = request.Headers.Authorization?.Parameter;
            this.RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }

    private sealed class SequenceResponseHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Body)> responses;

        public SequenceResponseHandler(params (HttpStatusCode StatusCode, string Body)[] responses)
            => this.responses = new(responses);

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.RequestBodies.Add(await Assert.IsAssignableFrom<HttpContent>(request.Content).ReadAsStringAsync(cancellationToken));
            var response = this.responses.Dequeue();
            return new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }
}
